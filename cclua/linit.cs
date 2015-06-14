using System;

using luaL_Reg = cclua.lua530.luaL_Reg;


/*
** If you embed Lua in your program and need to open the standard
** libraries, call luaL_openlibs in your program. If you need a
** different set of libraries, copy this file to your project and edit
** it to suit your needs.
**
** You can also *preload* libraries, so that a later 'require' can
** open the library, which is already linked to the application.
** For that, do the following code:
**
**  luaL_getsubtable(L, LUA_REGISTRYINDEX, "_PRELOAD");
**  lua_pushcfunction(L, luaopen_modname);
**  lua_setfield(L, -2, modname);
**  lua_pop(L, 1);  // remove _PRELOAD table
*/

namespace cclua {
	
	public static partial class imp {

		/*
		** these libs are loaded by lua.c and are readily available to any Lua
		** program
		*/
		public static luaL_Reg[] loadedlibs = {
			new luaL_Reg ("_G", mod.luaopen_base),
			new luaL_Reg (LUA_LOADLIBNAME, mod.luaopen_package),
			new luaL_Reg (LUA_COLIBNAME, mod.luaopen_coroutine),
			new luaL_Reg (LUA_TABLIBNAME, mod.luaopen_table),
			new luaL_Reg (LUA_IOLIBNAME, mod.luaopen_io),
			new luaL_Reg (LUA_OSLIBNAME, mod.luaopen_os),
			new luaL_Reg (LUA_STRLIBNAME, mod.luaopen_string),
			new luaL_Reg (LUA_MATHLIBNAME, mod.luaopen_math),
			new luaL_Reg (LUA_UTF8LIBNAME, mod.luaopen_utf8),
			new luaL_Reg (LUA_DBLIBNAME, mod.luaopen_debug),
			new luaL_Reg (null, null),
		};
	}
	
	public static partial class lua530 {

		public static void luaL_openlibs (lua_State L) {
			luaL_Reg[] lib = imp.loadedlibs;
			/* "require" functions from 'loadedlibs' and set results to global table */
			for (int i = 0; lib[i].func != null; i++) {
				luaL_requiref (L, lib[i].name, lib[i].func, 1);
				lua_pop (L, 1);  /* remove lib */
			}
		}
	}

}
