using System;

namespace cclua53
{
    public static partial class imp {

		private static class ldo {
			
			public class LuaException : Exception {
				public lua_longjmp lj;
				public LuaException (lua_longjmp jmp) {
					lj = jmp;
				}
			}

			public static void LUAI_TRY (cclua.lua_State L, Pfunc f, object ud, lua_longjmp lj) {
				try {
					f (L, ud);
				}
				catch (Exception) {
					if (lj.status == 0) lj.status = -1;
				}
			}

            public static void LUAI_THROW (cclua.lua_State L, lua_longjmp lj) {
				throw (new LuaException (lj));
			}

			public static void abort () {
				// TODO
			}

			public static void seterrorobj (cclua.lua_State L, int errcode, int oldtop) {
				switch (errcode) {
				case cclua.LUA_ERRMEM: {  /* memory error? */
					setsvalue2s (L, oldtop, G (L).memerrmsg);  /* reuse preregistered msg. */
					break;					         
				}
				case cclua.LUA_ERRERR: {
					setsvalue2s (L, oldtop, luaS_newliteral (L, "error in error handling"));
					break;
				}
				default: {
					setobjs2s(L, oldtop, L->top - 1);  /* error message on current top */
					break;
				}
				}
				L.top = oldtop + 1;
			}
		}

        /* chain list of long jump buffers */
        public class lua_longjmp {
            public lua_longjmp previous;
            public volatile int status;  /* error code */
        }

		public static void luaD_throw (cclua.lua_State L, int errcode) {
			if (L.errorJmp != null) {  /* thread has an error handler? */
				L.errorJmp.status = errcode;
				LUAI_THROW (L, L.errorJmp);
			}
			else {  /* thread has no error handler */
				global_State g = G (L);
				L.status = (byte)errcode;  /* mark it as dead */
				if (g.mainthread.errorJmp != null) {  /* main thread has a handler? */
					setobjs2s (L, g.mainthread.top++, L.top - 1);  /* copy error obj. */
					luaD_throw (g.mainthread, errcode);  /* re-throw in main thread */
				}
				else {  /* no handler at all; abort */
					if (g.panic != null) {  /* panic function? */
						seterrorobj (L, errcode, L.top);  /* assume EXTRA_STACK */
						if (L.ci.top < L.top)
							L.ci.top = L.top;  /* pushing msg. can break this invariant */
						lua_unlock (L);
						g.panic (L);  /* call panic function (last chance to jump out) */
					}
					ldo.abort ();
				}
			}
		}

        /* type of protected functions, to be ran by 'runprotected' */
        public delegate void Pfunc (cclua.lua_State L, object ud);

		public static int luaD_rawrunprotected (cclua.lua_State L, Pfunc f, object ud) {
            ushort oldCcalls = L.nCcalls;
            lua_longjmp lj = new lua_longjmp ();
            lj.status = cclua.LUA_OK;
            lj.previous = L.errorJmp;  /* chain new error handler */
            L.errorJmp = lj;
			LUAI_TRY (L, f, ud, lj);
            L.errorJmp = lj.previous;  /* restore old error handler */
            L.nCcalls = oldCcalls;
            return lj.status;
        }
    }
}
