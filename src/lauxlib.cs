using System;

using lua_State = cclua.lua530.lua_State;
using lua_Debug = cclua.lua530.lua_Debug;

namespace cclua {
	
	public static partial class imp {

		/*
		** {======================================================
		** Traceback
		** =======================================================
		*/



		public const int LEVELS1 = 12;  /* size of the first part of the stack */
		public const int LEVELS2 = 10;  /* size of the second part of the stack */



		/*
		** search for 'objidx' in table at index -1.
		** return 1 + string at top if find a good name.
		*/
		public static bool findfield (lua_State L, int objidx, int level) {
			if (level == 0 || lua530.lua_istable (L, -1))
				return false;  /* not found */
			lua530.lua_pushnil (L);  /* start 'next' loop */
			while (lua530.lua_next (L, -2) != 0) {  /* for each pair in table */
				if (lua530.lua_type (L, -2) == lua530.LUA_TSTRING) {  /* ignore non-string keys */
					if (lua530.lua_rawequal (L, objidx, -1)) {  /* found object? */
						lua530.lua_pop (L, 1);  /* remove value (but keep name) */
						return true;
					}
					else if (findfield (L, objidx, level - 1)) {  /* try recursively */
						lua530.lua_remove (L, -2);  /* remove table (but keep name) */
						lua530.lua_pushliteral (L, ".");
						lua530.lua_insert (L, -2);  /* place '.' between the two names */
						lua530.lua_concat (L, 3);
						return true;
					}
				}
				lua530.lua_pop (L, 1);  /* remove value */
			}
			return false;  /* not found */
		}


		/*
		** Search for a name for a function in all loaded modules
		** (registry._LOADED).
		*/
		public static bool pushglobalfuncname (lua_State L, lua_Debug ar) {
			int top = lua530.lua_gettop (L);
			lua530.getinfo (L, "f", ar);  /* push function */
			lua530.lua_getfield (L, lua530.LUA_REGISTRYINDEX, "_LOADED");
			if (findfield (L, top + 1, 2)) {
				string name = lua530.lua_tostring (L, -1);
				if (strncmp (name, "_G.", 3) == 0) {  /* name start with '_G.'? */
					lua530.lua_pushstring (name + 3);  /* push name without prefix */
					lua530.lua_remove (L, -2);  /* remove original name */
				}
				lua530.lua_copy (L, -1, top + 1);  /* move name to proper place */
				lua530.lua_pop (L, 2);  /* remove pushed values */
				return true;
			}
			else {
				lua530.lua_settop (L, top);  /* remove function and global table */
				return false;
			}
		}


		public static void pushfuncname (lua_State L, lua_Debug ar) {
			if (pushglobalfuncname (L, ar)) {  /* try first a global name */
				lua530.pushfstring (L, "function '%s'", lua530.lua_tostring (L, -1));
				lua530.lua_remove (L, -2);  /* remove name */
			}
			else if (ar.namewhat != '\0')  /* is there a name from code? */
				lua530.lua_pushfstring (L, "%s %s", ar.namewhat, ar.name);  /* use it */
			else if (ar.what == 'm')  /* main? */
				lua530.lua_pushliteral (L, "main chunk");
			else if (ar.what != 'C')  /* for Lua functions, use <file:line> */
				lua530.lua_pushfstring (L, "function <%s:%d>", ar.short_src, ar.linedefined);
			else  /* nothing left... */
				lua530.lua_pushliteral (L, "?");
		}


		public static int countlevels (lua_State L) {
			lua_Debug ar = new lua_Debug ();
			int li = 1;
			int le = 1;
			while (lua530.lua_getstack (L, le, ar)) { li = le; le *= 2; }
			while (li < le) {
				int m = (li + le) / 2;
				if (lua530.lua_getstack (L, m, ar)) li = m + 1;
				else le = m;
			}
			return le - 1;
		}












		
		public static int panic (lua_State L) {
			imp.lua_writestringerror ("PANIC: unprotected error in call to Lua API (%s)\n", lua_tostring (L, -1));
			return 0; /* return to Lua to abort */
		}
	}

    public static partial class lua530 {

		/* extra error code for 'luaL_load' */
		public const int LUA_ERRFILE = LUA_ERRERR + 1;


		public class luaL_Reg {
			public string name;
			public lua_CFunction func;
		}


		public const int LUAL_NUMSIZES = (sizeof (long) * 16 + sizeof (double));


		public static void luaL_checkversion (lua_State L) { luaL_checkversion_ (L, LUA_VERSION_NUM, LUAL_NUMSIZES); }


		/* pre-defined references */
		public const int LUA_NOREF = -2;
		public const int LUA_REFNIL = -1;


		public static void luaL_loadfile (lua_State L, string f) { luaL_loadfilex (L, f, null); }


		/*
		** ===============================================================
		** some useful macros
		** ===============================================================
		*/


		public static void luaL_newlibtable (lua_State L, luaL_Reg[] l) { lua_createtable (L, 0, l.Count); }

		public static void luaL_newlib (lua_State L, luaL_Reg[] l) { luaL_checkversion (L); luaL_newlibtable (L, l); luaL_setfuncs (L, l, 0); }

		public static void luaL_argcheck (lua_State L, bool cond, int arg, string extramsg) { if (cond == false) luaL_argerror (L, arg, extramsg); }
		public static void luaL_checkstring (lua_State L, int n) { luaL_checklstring (L, n, null); }
		public static void luaL_optstring (lua_State L, int n, int d) { luaL_optlstring (L, n, d, null); }

		public static string luaL_typename (lua_State L, int i) { return lua_typename (L, lua_type (L, i)); }

		public static void luaL_dofile (lua_State L, string fn) { if (luaL_loadfile (L, fn) == 0) lua_pcall (L, 0, LUA_MULTRET, 0); }

		public static void luaL_dostring (lua_State L, byte[] s) { if (luaL_loadstring (L, s) == 0) lua_pcall (L, 0, LUA_MULTRET, 0); }

		public static void luaL_getmetatable (lua_State L, int n) { lua_getfield (L, LUA_REGISTRYINDEX, n); }

		public static int luaL_opt (lua_State L, lua_CFunction f, int n, int d) { return (lua_isnoneornil (L, n) ? d : f (L, n)); }

		public static void luaL_loadbuffer (lua_State L, byte[] s, int sz, int n) { luaL_loadbufferx (L, s, sz, n, null); }


		/*
		** {======================================================
		** Generic Buffer manipulation
		** =======================================================
		*/


		public class luaL_Buffer {
			public byte[] b;
			public int size;
			public int n;
			lua_State L;
			byte[] initb;

			public luaL_Buffer () {
				initb = new byte[LUAL_BUFFERSIZE];
			}
		}

		public static void luaL_addchar (luaL_Buffer b, byte c) {
			if (b.n >= b.size) luaL_prepbuffsize (b, 1);
			b.b[b.n++] = c;
		}

		public static void luaL_addsize (luaL_Buffer b, int s) { b.n += s; }

		public static void luaL_prepbuffer (luaL_Buffer b) { luaL_prepbuffsize (b, LUAL_BUFFERSIZE); }



		public static void luaL_traceback (lua_State L, lua_State L1, string msg, int level) {
			lua_Debug ar;
			int top = lua_gettop (L);
			int numlevels = imp.countlevels (L1);
			int mark = (numlevels > imp.LEVELS1 + imp.LEVELS2) ? imp.LEVELS1 : 0;
			if (msg != null) lua_pushfstring (L, "%s\n", msg);
			lua_pushliteral (L, "stack traceback:");

		}
		
















        public static string lua_tolstring (lua_State L, int index, ref ulong len) {
            // TODO
            return "";
        }

        public static lua_State luaL_newstate () {
            lua_State L = lua_newstate ();
            if (L == null) lua_atpanic (L, lauxlib.panic);
            return L;
        }
    }
}
