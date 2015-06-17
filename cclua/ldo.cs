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
            ** Completes the execution of an interrupted C function, calling its
            ** continuation function.
            */
            public static void finishCcall (lua_State L, int status) {
                CallInfo ci = L.ci;
                /* must have a continuation and must be able to call it */
                lua_assert (ci.u.c.k != null && L.nny == 0);
                /* error status can only happen in a protected call */
                lua_assert (((ci.callstatus & CIST_YPCALL) != 0) || status == cc.LUA_YIELD);
                if ((ci.callstatus & CIST_YPCALL) != 0) {  /* was inside a pcall? */
                    ci.callstatus = (byte)(ci.callstatus & (~CIST_YPCALL));  /* finish 'lua_pcall' */
                    L.errfunc = ci.u.c.old_errfunc;
                }
                /* finish 'lua_callk'/'lua_pcall'; CIST_YPCALL and 'errfunc' already
                    handled */
                adjustresults (L, ci.nresults);
                /* call continuation function */
                cc.lua_unlock (L);
                int n = ci.u.c.k (L, status, ci.u.c.ctx);
                cc.lua_lock (L);
                api_checknelems (L, n);
                /* finish 'luaD_precall' */
                luaD_poscall (L, L.top - n);
            }


            /*
            ** Try to find a suspended protected call (a "recover point") for the
            ** given thread.
            */
            public static CallInfo findpcall (lua_State L) {
                for (CallInfo ci = L.ci; ci != null; ci = ci.previous) {
                    if ((ci.callstatus & CIST_YPCALL) != 0)
                        return ci;
                }
                return null;
            }


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


        public static bool errorstatus (int status) { return status > cc.LUA_YIELD; }


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
                    setobjs2s (L, L.stack[oldtop], L.stack[L.top - 1]);  /* error message on current top */
                    break;
                }
            }
            L.top = oldtop + 1;
        }
			
			
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
						seterrorobj (L, errcode, L.top);  /* assume EXTRA_STACK */
						if (L.ci.top < L.top)
							L.ci.top = L.top;  /* pushing msg. can break this invariant */
                        cc.lua_unlock (L);
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
                cc.lua_unlock (L);
                hook (L, ar);
                cc.lua_lock (L);
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
                cc.lua_unlock (L);
                n = f (L);  /* do the actual call */
                cc.lua_lock (L);
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
            }
            if (allowyield == 0) L.nny++;
            if (luaD_precall (L, func, nResults) == 0)  /* is a Lua function? */
                luaV_execute (L);  /* call it */
            if (allowyield == 0) L.nny--;
            L.nCcalls--;
        }


        /*
        ** Executes "full continuation" (everything in the stack) of a
        ** previously interrupted coroutine until the stack is empty (or another
        ** interruption long-jumps out of the loop). If the coroutine is
        ** recovering from an error, 'ud' points to the error status, which must
        ** be passed to the first continuation function (otherwise the default
        ** status is LUA_YIELD).
        */
        public static void unroll (lua_State L, object ud) {
            if (ud != null)  /* error status? */
                ldo.finishCcall (L, (int)ud);  /* finish 'lua_pcallk' callee */
            while (L.ci != L.base_ci) {  /* something in the stack */
                if (isLua (L.ci))  /* C function? */
                    ldo.finishCcall (L, cc.LUA_YIELD);  /* complete its execution */
                else {  /* Lua function */
                    luaV_finishOp (L);  /* finish interrupted instruction */
                    luaV_execute (L);  /* execute down to higher C 'boundary' */
                }
            }
        }


        /*
        ** Recovers from an error in a coroutine. Finds a recover point (if
        ** there is one) and completes the execution of the interrupted
        ** 'luaD_pcall'. If there is no recover point, returns zero.
        */
        public static bool recover (lua_State L, int status) {
            CallInfo ci = ldo.findpcall (L);
            if (ci == null) return false;  /* no recovery point */
            /* "finish" luaD_pcall */
            int oldtop = restorestack (L, (int)ci.extra);
            luaF_close (L, oldtop);
            seterrorobj (L, status, oldtop);
            L.ci = ci;
            L.allowhook = (byte)getoah (ci.callstatus);  /* restore original 'allowhook' */
            L.nny = 0;  /* should be zero to be yieldable */
            luaD_shrinkstack (L);
            L.errfunc = ci.u.c.old_errfunc;
            return true;  /* continue running the coroutine */
        }


        /*
        ** signal an error in the call to 'resume', not in the execution of the
        ** coroutine itself. (Such errors should not be handled by any coroutine
        ** error handler and should not kill the coroutine.)
        */
        public static void resume_error (lua_State L, string msg, int firstArg) {
            L.top = firstArg;  /* remove args from the stack */
            setsvalue2s (L, L.top, luaS_new (L, msg));  /* push error message */
            api_incr_top (L);
            luaD_throw (L, -1);  /* jump back to 'lua_resume' */
        }


        /*
        ** Do the work for 'lua_resume' in protected mode. Most of the work
        ** depends on the status of the coroutine: initial state, suspended
        ** inside a hook, or regularly suspended (optionally with a continuation
        ** function), plus erroneous cases: non-suspended coroutine or dead
        ** coroutine.
        */
        public static void resume (lua_State L, object ud) {
            ushort nCcalls = L.nCcalls;
            int firstArg = (int)ud;
            CallInfo ci = L.ci;
            if (nCcalls >= LUAI_MAXCCALLS)
                resume_error (L, "C stack overflow", firstArg);
            if (L.status == cc.LUA_OK) {  /* may be starting a coroutine */
                if (ci != L.base_ci)  /* not in base level? */
                    resume_error (L, "cannot resume non-suspended coroutine", firstArg);
                /* coroutine is in base level; start running it */
                if (luaD_precall (L, firstArg - 1, cc.LUA_MULTRET) == 0)  /* Lua function? */
                    luaV_execute (L);  /* call it */
            }
            else if (L.status != cc.LUA_YIELD)
                resume_error (L, "cannot resume dead coroutine", firstArg);
            else {  /* resuming from previous yield */
                L.status = cc.LUA_OK;  /* mark that it is running (again) */
                ci.func = restorestack (L, (int)ci.extra);
                if (isLua (ci))  /* yielded inside a hook? */
                    luaV_execute (L);  /* just continue running Lua code */
                else {  /* 'common' yield */
                    if (ci.u.c.k != null) {  /* does it have a continuation function? */
                        cc.lua_unlock (L);
                        int n = ci.u.c.k (L, cc.LUA_YIELD, ci.u.c.ctx);  /* call continuation */
                        cc.lua_lock (L);
                        api_checknelems (L, n);
                        firstArg = L.top - n;  /* yield results come from continuation */
                    }
                    luaD_poscall (L, firstArg);  /* finish 'luaD_precall' */
                }
                unroll (L, null);  /* run continuation */
            }
            lua_assert (nCcalls == L.nCcalls);
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
                seterrorobj (L, status, oldtop);
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


        public static int lua_resume (lua_State L, lua_State from, int nargs) {
            ushort oldnny = L.nny;  /* save "number of non-yieldable" calls */
            lua_lock (L);
            imp.luai_userstateresume (L, nargs);
            L.nCcalls = (ushort)((from != null) ? from.nCcalls + 1 : 1);
            L.nny = 0;  /* allow yields */
            imp.api_checknelems (L, (L.status == LUA_OK) ? nargs + 1 : nargs);
            int status = imp.luaD_rawrunprotected (L, imp.resume, L.top - nargs);
            if (status == -1)  /* error calling 'lua_resume'? */
                status = LUA_ERRRUN;
            else {  /* continue running after recoverable errors */
                while (imp.errorstatus (status) && imp.recover (L, status)) {
                    /* unroll continuation */
                    status = imp.luaD_rawrunprotected (L, imp.unroll, status);
                }
                if (imp.errorstatus (status)) {  /* unrecoverable error? */
                    L.status = (byte)status;  /* mark thread as 'dead' */
                    imp.seterrorobj (L, status, L.top);  /* push error message */
                    L.ci.top = L.top;
                }
                else lua_assert (status == L.status);  /* normal end or yield */
            }
            L.nny = oldnny;  /* restore 'nny' */
            L.nCcalls--;
            lua_assert (L.nCcalls == ((from != null) ? from.nCcalls : 0));
            lua_unlock (L);
            return status;
        }


        public static int lua_isyieldable (lua_State L) {
            return ((L.nny == 0) ? 1 : 0);
        }


        public static int lua_yieldk (lua_State L, int nresults, long ctx, lua_KFunction k) {
            CallInfo ci = L.ci;
            imp.luai_userstateyield (L, nresults);
            lua_lock (L);
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
            lua_unlock (L);
            return 0;  /* return to 'luaD_hook' */
        }
	}
}
