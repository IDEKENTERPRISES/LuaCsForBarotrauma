﻿using System;
using System.Collections.Generic;
using System.Text;
using System.IO;
using Barotrauma.Networking;
using MoonSharp.Interpreter;
using Microsoft.Xna.Framework;
using System.Threading.Tasks;
using Barotrauma.Items.Components;
using System.Diagnostics;
using System.Linq;
using MoonSharp.Interpreter.Interop;
using System.Reflection;
using FarseerPhysics.Dynamics;
using System.IO.Compression;
using HarmonyLib;

#if CLIENT
using Microsoft.Xna.Framework.Graphics;
using EventInput;
using Microsoft.Xna.Framework.Input;
#endif

namespace Barotrauma
{
	partial class LuaSetup
	{
		public static LuaSetup luaSetup;

		public Script lua;

		public LuaHook hook;
		public LuaGame game;
		public LuaNetworking networking;
		public Harmony harmony;

		public LuaScriptLoader luaScriptLoader;

		public void Update()
		{
			hook?.Update();
		}

		public void HandleLuaException(Exception ex, string extra = "")
		{
			if (!string.IsNullOrWhiteSpace(extra))
				PrintError(extra);

			if (ex is InterpreterException)
			{
				if (((InterpreterException)ex).DecoratedMessage == null)
					PrintError(((InterpreterException)ex).Message);
				else
					PrintError(((InterpreterException)ex).DecoratedMessage);
			}
			else
			{
				PrintError(ex.ToString());
			}
		}

		public void PrintError(object message)
		{
			if (message == null) { message = "nil"; }
			string str = message.ToString();

			for (int i = 0; i < str.Length; i += 1024)
			{
				string subStr = str.Substring(i, Math.Min(1024, str.Length - i));

				string errorMsg = subStr;
				if (i == 0)
					errorMsg = "[LUA ERROR] " + errorMsg;

				DebugConsole.ThrowError(errorMsg);

#if SERVER
				if (GameMain.Server != null)
				{
					foreach (var c in GameMain.Server.ConnectedClients)
					{
						GameMain.Server.SendDirectChatMessage(errorMsg, c, ChatMessageType.Console);
					}

					GameServer.Log(errorMsg, ServerLog.MessageType.Error);
				}
#endif
			}
		}

		public void PrintMessage(object message)
		{
			if (message == null) { message = "nil"; }
			string str = message.ToString();

			Console.WriteLine(str);

			for (int i = 0; i < str.Length; i += 1024)
			{
				string subStr = str.Substring(i, Math.Min(1024, str.Length - i));


#if SERVER
				if (GameMain.Server != null)
				{
					foreach (var c in GameMain.Server.ConnectedClients)
					{
						GameMain.Server.SendDirectChatMessage(subStr, c, ChatMessageType.Console);
					}

					GameServer.Log("[LUA] " + subStr, ServerLog.MessageType.ServerMessage);
				}
#else
			DebugConsole.NewMessage("[LUA] " + message.ToString());
#endif
			}

		}

		public void PrintMessageNoLog(object message)
		{
			if (message == null) { message = "nil"; }
			Console.WriteLine(message.ToString());
		}

		public DynValue DoString(string code, Table globalContext = null, string codeStringFriendly = null)
		{
			try
			{
				return lua.DoString(code, globalContext, codeStringFriendly);
			}
			catch (Exception e)
			{
				HandleLuaException(e);
			}

			return null;
		}

		public DynValue DoFile(string file, Table globalContext = null, string codeStringFriendly = null)
		{
			if(!LuaFile.IsPathAllowedLuaException(file)) return null;

			try
			{
				return lua.DoFile(file, globalContext, codeStringFriendly);

			}
			catch (Exception e)
			{
				HandleLuaException(e);
			}

			return null;
		}


		public DynValue LoadString(string file, Table globalContext = null, string codeStringFriendly = null)
		{
			try
			{
				return lua.LoadString(file, globalContext, codeStringFriendly);

			}
			catch (Exception e)
			{
				HandleLuaException(e);
			}

			return null;
		}

		public DynValue LoadFile(string file, Table globalContext = null, string codeStringFriendly = null)
		{
			if (!LuaFile.IsPathAllowedLuaException(file)) return null;

			try
			{
				return lua.LoadFile(file, globalContext, codeStringFriendly);

			}
			catch (Exception e)
			{
				HandleLuaException(e);
			}

			return null;
		}

		public DynValue Require(string modname, Table globalContext)
		{
			try
			{
				return lua.Call(lua.RequireModule(modname, globalContext));

			}
			catch (Exception e)
			{
				HandleLuaException(e);
			}

			return null;
		}

		public static DynValue CreateUserDataSafe(object o)
		{
			if (o == null)
				return DynValue.Nil;

			return UserData.Create(o);
		}


		public object CallFunction(object function, params object[] arguments)
		{
			try
			{
				return lua.Call(function, arguments);
			}
			catch (Exception e)
			{
				HandleLuaException(e);
			}

			return null;
		}

		public void SetModulePaths(string[] str)
		{
			luaScriptLoader.ModulePaths = str;
		}

		public float TestFunction(float value)
		{
			return value * 2;
		}

		// messy solution
		public object HandleCall(object arg1, ScriptExecutionContext arg2, CallbackArguments arg3)
		{
			
			var what = arg3.RawGet(0, true);

			var code = "return " + what.UserData.Descriptor.Type.Name + ".__new(";

			var tbl = new Table(lua);
			tbl[what.UserData.Descriptor.Type.Name] = what;
			
			for(var i=1; i < arg3.Count; i++)
			{
				if (i == arg3.Count - 1)
					code = code + "arg" + i;
				else
					code = code + "arg" + i + ",";

				tbl["arg" + i] = arg3.RawGet(i, true);
			}

			code = code + ")";

			try
			{
				return lua.DoString(code, tbl);
			}
			catch(Exception e)
			{
				HandleLuaException(e);
			}

			return null;
		}


#if SERVER
		public static void InstallClientSideLua()
		{
			if (!File.Exists("Mods/LuaForBarotrauma/clientside_files.zip"))
			{
				GameMain.Server.SendChatMessage("clientside_files.zip doesn't exist, Github version?", ChatMessageType.ServerMessageBox);

				return;
			}

			try
			{

				ZipFile.ExtractToDirectory("Mods/LuaForBarotrauma/clientside_files.zip", ".", true);

				File.Move("Barotrauma.dll", "Barotrauma.dll.temp", true);
				File.Move("Barotrauma.deps.json", "Barotrauma.deps.json.temp", true);
				File.Move("RestSharp.dll", "RestSharp.dll.temp", true);

				File.Move("Barotrauma.dll.original", "Barotrauma.dll");
				File.Move("Barotrauma.deps.json.original", "Barotrauma.deps.json");
				File.Move("RestSharp.dll.original", "RestSharp.dll", true);

				File.Move("Barotrauma.dll.temp", "Barotrauma.dll.original", true);
				File.Move("Barotrauma.deps.json.temp", "Barotrauma.deps.json.original", true);
			}
			catch (Exception e)
			{
				LuaSetup.luaSetup.HandleLuaException(e);

				return;
			}

			GameMain.Server.SendChatMessage("Client-Side Lua installed, restart your game to apply changes.", ChatMessageType.ServerMessageBox);
		}

#endif

		public void Stop()
		{
			if (harmony != null)
				harmony.UnpatchAll();

			game.Stop();
			hook.Call("stop", new object[] { });

			hook = new LuaHook(null);
			game = new LuaGame(null);
			networking = new LuaNetworking(null);
			luaScriptLoader = null;

			luaSetup = null;

		}

		public void Initialize()
		{
			Stop();
			
			luaSetup = this;

			PrintMessage("Lua! Version " + AssemblyInfo.GitRevision);

			luaScriptLoader = new LuaScriptLoader(this);
			luaScriptLoader.ModulePaths = new string[] { };

			LuaCustomConverters.RegisterAll();

			lua = new Script(CoreModules.Preset_SoftSandbox);
			lua.Options.DebugPrint = PrintMessage;
			lua.Options.ScriptLoader = luaScriptLoader;

			harmony = new Harmony("com.LuaForBarotrauma");
			harmony.UnpatchAll();

			hook = new LuaHook(this);
			game = new LuaGame(this);
			networking = new LuaNetworking(this);

			UserData.RegisterType<LuaHook>();
			UserData.RegisterType<LuaGame>();
			UserData.RegisterType<LuaRandom>();
			UserData.RegisterType<LuaTimer>();
			UserData.RegisterType<LuaFile>();
			UserData.RegisterType<LuaNetworking>();
			UserData.RegisterType<LuaUserData>();
			UserData.RegisterType<IUserDataDescriptor>();
			


#if SERVER

#elif CLIENT
			UserData.RegisterType<LuaGUI>();
#endif

			lua.Globals["setmodulepaths"] = (Action<string[]>)SetModulePaths;

			lua.Globals["TestFunction"] = (Func<float, float>)TestFunction;

			lua.Globals["printNoLog"] = (Action<object>)PrintMessageNoLog;

			lua.Globals["dofile"] = (Func<string, Table, string, DynValue>)DoFile;
			lua.Globals["loadfile"] = (Func<string, Table, string, DynValue>)LoadFile;
			lua.Globals["require"] = (Func<string, Table, DynValue>)Require;

			lua.Globals["dostring"] = (Func<string, Table, string, DynValue>)DoString;
			lua.Globals["load"] = (Func<string, Table, string, DynValue>)LoadString;
			
			lua.Globals["LuaUserData"] = UserData.CreateStatic<LuaUserData>();
			lua.Globals["Game"] = game;
			lua.Globals["Hook"] = hook;
			lua.Globals["Random"] = new LuaRandom();
			lua.Globals["Timer"] = new LuaTimer(this);
			lua.Globals["File"] = UserData.CreateStatic<LuaFile>();
			lua.Globals["Networking"] = networking;
			
#if SERVER

#elif CLIENT
			lua.Globals["GUI"] = new LuaGUI(this);
#endif

			bool isServer = true;

#if SERVER
			isServer = true;
#else
			isServer = false;
#endif

			lua.Globals["SERVER"] = isServer;
			lua.Globals["CLIENT"] = !isServer;

			// LuaDocs.GenerateDocsAll();

			if (File.Exists("Lua/LuaSetup.lua")) // try the default loader
				DoFile("Lua/LuaSetup.lua");
			else if (File.Exists("Mods/LuaForBarotrauma/Lua/LuaSetup.lua")) // in case its the workshop version
				DoFile("Mods/LuaForBarotrauma/Lua/LuaSetup.lua");
			else
				PrintError("Lua loader not found! Lua/LuaSetup.lua, no lua scripts will be executed or work.");
		}

		public LuaSetup()
		{
			hook = new LuaHook(null);
			game = new LuaGame(null);
			networking = new LuaNetworking(null);
		}

	}



}

