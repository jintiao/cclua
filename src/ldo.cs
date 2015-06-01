using System;

using lua_State = cclua.lua530.lua_State;
using lua_longjmp = cclua.lua530.lua_longjmp;

namespace cclua {

    public static partial class imp {

        private static class ldo {

            /* some space for error handling */
            public const int ERRORSTACKSIZE = LUAI_MAXSTACK + 200;

			
			public class LuaException : Exception {
				public lua_longjmp lj;
				public LuaException (lua_longjmp jmp) {
					lj = jmp;
				}
			}

			public static void LUAI_TRY (lua_State L, Pfunc f, object ud, lua_longjmp lj) {
				try {
					f (L, ud);
				}
				catch (Exception) {
					if (lj.status == 0) lj.status = -1;
				}
			}

            public static void LUAI_THROW (lua_State L, lua_longjmp lj) {
				throw (new LuaException (lj));
			}

			public static void abort () {
				// TODO
			}

			public static void seterrorobj (lua_State L, int errcode, int oldtop) {
				switch (errcode) {
				case lua530.LUA_ERRMEM: {  /* memory error? */
					setsvalue2s (L, L.stack[oldtop], G (L).memerrmsg);  /* reuse preregistered msg. */
					break;					         
				}
				case lua530.LUA_ERRERR: {
					setsvalue2s (L, L.stack[oldtop], luaS_newliteral (L, "error in error handling"));
					break;
				}
				default: {
					setobjs2s(L, L.stack[oldtop], L.stack[L.top - 1]);  /* error message on current top */
					break;
				}
				}
				L.top = oldtop + 1;
			}


            public static void correctstack (lua_State L, TValue[] oldstack) {
                // TODO
            }


		}

		public static void luaD_throw (lua_State L, int errcode) {
			if (L.errorJmp != null) {  /* thread has an error handler? */
				L.errorJmp.status = errcode;
				ldo.LUAI_THROW (L, L.errorJmp);
			}
			else {  /* thread has no error handler */
				global_State g = G (L);
				L.status = (byte)errcode;  /* mark it as dead */
				if (g.mainthread.errorJmp != null) {  /* main thread has a handler? */
                    setobjs2s (L, g.mainthread.stack[g.mainthread.top++], L.stack[L.top - 1]);  /* copy error obj. */
					luaD_throw (g.mainthread, errcode);  /* re-throw in main thread */
				}
				else {  /* no handler at all; abort */
					if (g.panic != null) {  /* panic function? */
						ldo.seterrorobj (L, errcode, L.top);  /* assume EXTRA_STACK */
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
        public delegate void Pfunc (lua_State L, object ud);

		public static int luaD_rawrunprotected (lua_State L, Pfunc f, object ud) {
            ushort oldCcalls = L.nCcalls;
			lua_longjmp lj = luaM_newobject<lua_longjmp> (L);
            lj.status = lua530.LUA_OK;
            lj.previous = L.errorJmp;  /* chain new error handler */
            L.errorJmp = lj;
			ldo.LUAI_TRY (L, f, ud, lj);
            L.errorJmp = lj.previous;  /* restore old error handler */
            L.nCcalls = oldCcalls;
            return lj.status;
        }



        public static void luaD_reallocstack (lua_State L, int newsize) {
            TValue[] oldstack = L.stack;
            int lim = L.stacksize;
            lua_assert (newsize <= LUAI_MAXSTACK || newsize == ldo.ERRORSTACKSIZE);
            lua_assert (L.stack_last == L.stacksize - EXTRA_STACK);
            luaM_reallocvector (L, ref L.stack, L.stacksize, newsize);
            for (; lim < newsize; lim++)
                setnilvalue (L.stack[lim]);  /* erase new segment */
            L.stacksize = newsize;
            L.stack_last = newsize - EXTRA_STACK;
            ldo.correctstack (L, oldstack);
        }


        public static void luaD_growstack (lua_State L, int n) {
            int size = L.stacksize;
            if (size > LUAI_MAXSTACK)  /* error after extra size? */
                luaD_throw (L, lua530.LUA_ERRERR);
            else {
                int needed = L.top + n + EXTRA_STACK;
                int newsize = 2 * size;
                if (newsize > LUAI_MAXSTACK) newsize = LUAI_MAXSTACK;
                if (newsize < needed) newsize = needed;
                if (newsize > LUAI_MAXSTACK) {  /* stack overflow? */
                    luaD_reallocstack (L, ldo.ERRORSTACKSIZE);
                    luaG_runerror (L, "stack overflow");
                }
                else
                    luaD_reallocstack (L, newsize);
            }
        }
    }

	public static partial class lua530 {

		/* chain list of long jump buffers */
		public class lua_longjmp {
			public lua_longjmp previous;
			public volatile int status;  /* error code */
		}
	}
}
