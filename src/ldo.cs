using System;

using cc = cclua.lua530;

using lua_State = cclua.lua530.lua_State;
using lua_longjmp = cclua.lua530.lua_longjmp;
using lua_Hook = cclua.lua530.lua_Hook;
using lua_Debug = cclua.lua530.lua_Debug;
using CallInfo = cclua.imp.CallInfo;

namespace cclua {

    public static partial class imp {

        private static class ldo {

            /* some space for error handling */
            public const int ERRORSTACKSIZE = LUAI_MAXSTACK + 200;


			/*
			** {======================================================
			** Error-recovery functions
			** =======================================================
			*/

			/*
			** LUAI_THROW/LUAI_TRY define how Lua does exception handling. By
			** default, Lua handles errors with exceptions when compiling as
			** C++ code, with _longjmp/_setjmp when asked to use them, and with
			** longjmp/setjmp otherwise.
			*/

			public static void LUAI_TRY (lua_State L, Pfunc f, object ud, lua_longjmp lj) {
				try { f (L, ud); }
				catch (Exception) { if (lj.status == 0) lj.status = -1; }
			}

			public static void LUAI_THROW (lua_State L, lua_longjmp lj) { throw (new LuaException (lj)); }
			
			public class LuaException : Exception {
				public lua_longjmp lj;
				public LuaException (lua_longjmp jmp) { lj = jmp; }
			}

			public static void abort () { /* TODO */  }



			public static void seterrorobj (lua_State L, int errcode, int oldtop) {
				switch (errcode) {
				case cc.LUA_ERRMEM: {  /* memory error? */
					setsvalue2s (L, L.stack[oldtop], G (L).memerrmsg);  /* reuse preregistered msg. */
					break;					         
				}
				case cc.LUA_ERRERR: {
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
				for (UpVal up = L.openupval; up != null; up = up.u.open.next)
					up.v = L.stack[up.level];
            }


            public static int stackinuse (lua_State L) {
                int lim = L.top;
                for (CallInfo ci = L.ci; ci != null; ci = ci.previous) {
                    lua_assert (ci.top <= L.stack_last);
                    if (lim < ci.top) lim = ci.top;
                }
                return (lim + 1);  /* part of stack in use */
            }


            public static void callhook (lua_State L, CallInfo ci) {
                int hook = cc.LUA_HOOKCALL;
                ci.u.l.savedpc++;  /* hooks assume 'pc' is already incremented */
                luaD_hook (L, hook, -1);
                ci.u.l.savedpc--;
            }


            public static int adjust_varargs (lua_State L, Proto p, int actual) {
                int nfixargs = p.numparams;
                lua_assert (actual >= nfixargs);
                /* move fixed parameters to final position */
                luaD_checkstack (L, p.maxstacksize);  /* check again for new 'base' */
                int fix = L.top - actual;  /* first fixed argument */
                int b = L.top;  /* final position of first argument */
                for (int i = 0; i < nfixargs; i++) {
                    setobjs2s (L, L.stack[L.top++], L.stack[fix + i]);
                    setnilvalue (L.stack[fix + 1]);
                }
                return b;
            }


            /*
            ** Check whether __call metafield of 'func' is a function. If so, put
            ** it in stack below original 'func' so that 'luaD_precall' can call
            ** it. Raise an error if __call metafield is not a function.
            */
            public static void tryfuncTM (lua_State L, int func) {
                TValue tm = luaT_gettmbyobj (L, L.stack[func], TMS.TM_CALL);
                if (ttisfunction (tm) == false)
                    luaG_typeerror (L, L.stack[func], "call");
                /* Open a hole inside the stack at 'func' */
                for (int p = L.top; p > func; p--)
                    setobjs2s (L, L.stack[p], L.stack[p - 1]);
                L.top++;  /* slot ensured by caller */
                setobj2s (L, L.stack[func], tm);  /* tag method is the new function to be called */
            }


            public static CallInfo next_ci (lua_State L) { L.ci = (L.ci.next != null) ? L.ci.next : luaE_extendCI (L); return L.ci; }


            /*
            ** Execute a protected parser.
            */
            public class SParser {  /* data to 'f_parser' */
                public Zio z;
                public MBuffer buff;  /* dynamic structure used by the scanner */
                public Dyndata dyd;  /* dynamic structures used by the parser */
                public string mode;
                public string name;

                public SParser () {
                    buff = new MBuffer ();
                    dyd = new Dyndata ();
                }
            }


            public static void checkmode (lua_State L, string mode, string x) {
                if (mode != null && mode.IndexOf (x[0]) < 0) {
                    luaO_pushfstring (L,
                        "attempt to load a %s chunk (mode is '%s')", x, mode);
                    luaD_throw (L, cc.LUA_ERRSYNTAX);
                }
            }


            public static void f_parser (lua_State L, object ud) {
                LClosure cl = null;
                SParser p = (SParser)ud;
                int c = zgetc (p.z);  /* read first character */
                if (c == cc.LUA_SIGNATURE[0]) {
                }
                else {
                    checkmode (L, p.mode, "text");
                    cl = luaY_parser (L, p.z, p.buff, p.dyd, p.name, c);
                }
                lua_assert (cl.nupvalues == cl.p.sizeupvalues);
                luaF_initupvals (L, cl);
            }
		}


        public static int savestack (lua_State L, int p) { return p; }
        public static int restorestack (lua_State L, int n) { return n; }
			
			
		/* type of protected functions, to be ran by 'runprotected' */
		public delegate void Pfunc (lua_State L, object ud);


		public static void luaD_checkstack (lua_State L, int n) {
			if (L.stack_last - L.top <= n)
				luaD_growstack (L, n);
			else
				condmovestack (L);
		}


        public static void incr_top (lua_State L) { L.top++; luaD_checkstack (L, 0); }


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


		public static int luaD_rawrunprotected (lua_State L, Pfunc f, object ud) {
            ushort oldCcalls = L.nCcalls;
			lua_longjmp lj = luaM_newobject<lua_longjmp> (L);
            lj.status = cc.LUA_OK;
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
                luaD_throw (L, cc.LUA_ERRERR);
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


        public static void luaD_shrinkstack (lua_State L) {
            int inuse = ldo.stackinuse (L);
            int goodsize = inuse + (inuse / 8) + 2 * EXTRA_STACK;
            if (goodsize > LUAI_MAXSTACK) goodsize = LUAI_MAXSTACK;
            if (L.stacksize > LUAI_MAXSTACK)  /* was handling stack overflow? */
                luaE_freeCI (L);  /* free all CIs (list grew because of an error) */
            else
                luaE_shrinkCI (L);  /* shrink list */
            if (inuse > LUAI_MAXSTACK ||  /* still handling stack overflow? */
                goodsize >= L.stacksize)  /* would grow instead of shrink? */
                condmovestack (L);  /* don't change stack (change only for debugging) */
            else
                luaD_reallocstack (L, goodsize);  /* shrink it */
        }


        public static void luaD_hook (lua_State L, int ev, int line) {
            lua_Hook hook = L.hook;
            if (hook != null && L.allowhook != 0) {
                CallInfo ci = L.ci;
                int top = savestack (L, L.top);
                int ci_top = savestack (L, ci.top);
                lua_Debug ar = luaM_newobject<lua_Debug> (L);
                ar.ev = ev;
                ar.currentline = line;
                ar.i_ci = ci;
                luaD_checkstack (L, cc.LUA_MINSTACK);  /* ensure minimum stack size */
                ci.top = L.top + cc.LUA_MINSTACK;
                lua_assert (ci.top <= L.stack_last);
                L.allowhook = 0;  /* cannot call hooks inside a hook */
                ci.callstatus |= CIST_HOOKED;
                lua_unlock (L);
                hook (L, ar);
                lua_lock (L) ;
                lua_assert (L.allowhook == 0);
                L.allowhook = 1;
                ci.top = restorestack (L, ci_top);
                L.top = restorestack (L, top);
                ci.callstatus = (byte)((int)ci.callstatus & (~CIST_HOOKED));
            }
        }


        /*
        ** returns true if function has been executed (C function)
        */
        public static int luaD_precall (lua_State L, int func, int nresult) {
            CallInfo ci;
            int n;  /* number of arguments (Lua) or returns (C) */
            int funcr = savestack (L, func);
            int tt = ttype (L.stack[func]);
            if (tt == LUA_TLCF || tt == LUA_TCCL) {
                cc.lua_CFunction f = null;
                if (tt == LUA_TLCF) f = fvalue (L.stack[func]);  /* light C function */
                if (tt == LUA_TCCL) f = clCvalue (L.stack[func]).f;  /* C closure */
                luaD_checkstack (L, cc.LUA_MINSTACK);  /* ensure minimum stack size */
                ci = ldo.next_ci (L);  /* now 'enter' new function */
                ci.nresults = (short)nresult;
                ci.func = restorestack (L, funcr);
                ci.top = L.top + cc.LUA_MINSTACK;
                lua_assert (ci.top <= L.stack_last);
                ci.callstatus = 0;
                luaC_checkGC (L);  /* stack grow uses memory */
                if ((L.hookmask & cc.LUA_MASKCALL) != 0)
                    luaD_hook (L, cc.LUA_HOOKCALL, -1);
                lua_unlock (L);
                n = f (L);  /* do the actual call */
                lua_lock (L);
                api_checknelems (L, n);
                luaD_poscall (L, L.top - n);
                return 1;
            }
            else if (tt == LUA_TLCL) {  /* Lua function: prepare its call */
                Proto p = clLvalue (L.stack[func]).p;
                n = (L.top - func) - 1;  /* number of real arguments */
                int fbase;
                luaD_checkstack (L, p.maxstacksize);
                for (; nresult < p.numparams; n++)
                    setnilvalue (L.stack[L.top++]);  /* complete missing arguments */
                if (p.is_vararg == 0) {
                    func = restorestack (L, funcr);
                    fbase = func + 1;
                }
                else {
                    fbase = ldo.adjust_varargs (L, p, n);
                    func = restorestack (L, funcr);  /* previous call can change stack */
                }
                ci = ldo.next_ci (L);  /* now 'enter' new function */
                ci.nresults = (short)nresult;
                ci.func = func;
                ci.u.l.fbase = fbase;
                ci.top = fbase + p.maxstacksize;
                lua_assert (ci.top <= L.stack_last);
                ci.u.l.savedpc = 0;  /* starting point */
                ci.callstatus = CIST_LUA;
                L.top = ci.top;
                luaC_checkGC (L);  /* stack grow uses memory */
                if ((L.hookmask & cc.LUA_MASKCALL) != 0)
                    ldo.callhook (L, ci);
                return 0;

            }
            else {  /* not a function */
                luaD_checkstack (L, 1);  /* ensure space for metamethod */
                func = restorestack (L, funcr);  /* previous call may change stack */
                ldo.tryfuncTM (L, func);  /* try to get '__call' metamethod */
                return luaD_precall (L, func, nresult);  /* now it must be a function */
            }
        }


        public static int luaD_poscall (lua_State L, int firstResult) {
            CallInfo ci = L.ci;
            if ((L.hookmask & (cc.LUA_MASKRET | cc.LUA_MASKLINE)) != 0) {
                if ((L.hookmask & cc.LUA_MASKRET) != 0) {
                    int fr = savestack (L, firstResult);
                    luaD_hook (L, cc.LUA_HOOKRET, -1);
                    firstResult = restorestack (L, fr);
                }
                L.oldpc = ci.previous.u.l.savedpc;  /* 'oldpc' for caller function */
            }
            int res = ci.func;  /* res == final position of 1st result */
            int wanted = ci.nresults;
            L.ci = ci.previous;
            L.ci = ci.previous;  /* back to caller */
            /* move results to correct place */
            int i = wanted;
            for (; i != 0 && firstResult < L.top; i--)
                setobjs2s (L, L.stack[res++], L.stack[firstResult++]);
            while (i-- > 0)
                setnilvalue (L.stack[res++]);
            L.top = res;
            return (wanted - cc.LUA_MULTRET);  /* 0 iff wanted == LUA_MULTRET */
        }


        /*
        ** Call a function (C or Lua). The function to be called is at *func.
        ** The arguments are on the stack, right after the function.
        ** When returns, all the results are on the stack, starting at the original
        ** function position.
        */
        public static void luaD_call (lua_State L, int func, int nResults, int allowyield) {
            if ((++L.nCcalls) >= LUAI_MAXCCALLS) {
                if (L.nCcalls == LUAI_MAXCCALLS)
                    luaG_runerror (L, "C stack overflow");
                else if (L.nCcalls >= (LUAI_MAXCCALLS + (LUAI_MAXCCALLS >> 3)))
                    luaD_throw (L, cc.LUA_ERRERR);  /* error while handing stack error */
                if (allowyield == 0) L.nny++;
                if (luaD_precall (L, func, nResults) == 0)  /* is a Lua function? */
                    luaV_execute (L);  /* call it */
                if (allowyield == 0) L.nny--;
                L.nCcalls--;
            }
        }


        public static int luaD_pcall (lua_State L, Pfunc func, object u, int old_top, int ef) {
            CallInfo old_ci = L.ci;
            byte old_allowhooks = L.allowhook;
            ushort old_nny = L.nny;
            int old_errfunc = L.errfunc;
            L.errfunc = ef;
            int status = luaD_rawrunprotected (L, func, u);
            if (status != cc.LUA_OK) {  /* an error occurred? */
                int oldtop = restorestack (L, old_top);
                luaF_close (L, oldtop);  /* close possible pending closures */
                ldo.seterrorobj (L, status, oldtop);
                L.ci = old_ci;
                L.allowhook = old_allowhooks;
                L.nny = old_nny;
                luaD_shrinkstack (L);
            }
            L.errfunc = old_errfunc;
            return status;
        }



        public static int luaD_protectedparser (lua_State L, Zio z, string name, string mode) {
            L.nny++;
            ldo.SParser p = new ldo.SParser ();
            p.z = z;
            p.name = name;
            p.mode = mode;
            luaZ_initbuffer (L, p.buff);
            int status = luaD_pcall (L, ldo.f_parser, p, savestack (L, L.top), L.errfunc);
            luaZ_freebuffer (L, p.buff);
            luaM_freearray (L, p.dyd.actvar.arr);
            luaM_freearray (L, p.dyd.gt.arr);
            luaM_freearray (L, p.dyd.label.arr);
            L.nny--;
            return status;
        }


    }

	public static partial class lua530 {

		/* chain list of long jump buffers */
		public class lua_longjmp {
			public lua_longjmp previous;
			public volatile int status;  /* error code */
		}


        public static int lua_yieldk (lua_State L, int nresults, long ctx, lua_KFunction k) {
            CallInfo ci = L.ci;
            imp.luai_userstateyield (L, nresults);
            imp.lua_lock (L);
            imp.api_checknelems (L, nresults);
            if (L.nny > 0) {
                if (L != imp.G (L).mainthread)
                    imp.luaG_runerror (L, "attempt to yield across a C-call boundary");
                else
                    imp.luaG_runerror (L, "attempt to yield from outside a coroutine");
            }
            L.status = LUA_YIELD;
            ci.extra = imp.savestack (L, ci.func);  /* save current 'func' */
            if (imp.isLua (ci))  /* inside a hook? */
                imp.api_check (k == null, "hooks cannot continue after yielding");
            else {
                ci.u.c.k = k;  /* is there a continuation? */
                if (k != null) ci.u.c.ctx = ctx;  /* save context */
                ci.func = L.top - nresults - 1;  /* protect stack below results */
                imp.luaD_throw (L, LUA_YIELD);
            }
            imp.lua_assert ((ci.callstatus & imp.CIST_HOOKED) != 0);  /* must be inside a hook */
            imp.lua_unlock (L);
            return 0;  /* return to 'luaD_hook' */
        }
	}
}
