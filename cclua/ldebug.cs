using System;

using cc = cclua.lua530;

using lua_State = cclua.lua530.lua_State;
using lua_Debug = cclua.lua530.lua_Debug;
using CallInfo = cclua.imp.CallInfo;
using Closure = cclua.imp.Closure;

namespace cclua {

	public static partial class imp {

		private static class ldebug {

			public static bool noLuaClosure (Closure f) { return (f == null || f.c.tt == LUA_TCCL); }
		}

		public static int pcRel (int pc, Proto p) { return (pc - 1); }

		public static int getfuncline (Proto f, int pc) { return (f.lineinfo != null ? f.lineinfo[pc] : -1); }
		
		public static void resethookcount (lua_State L) { L.hookcount = L.basehookcount; }

		/* Active Lua function (given call info) */
		public static LClosure ci_func (lua_State L, CallInfo ci) { return clLvalue (L, ci.func); }



		public static int currentpc (lua_State L, CallInfo ci) {
			lua_assert (isLua (ci));
			return pcRel (ci.u.l.savedpc, ci_func (L, ci).p);
		}


		public static int currentline (lua_State L, CallInfo ci) {
			return getfuncline (ci_func (L, ci).p, currentpc (L, ci));
		}


		public static string upvalname (Proto p, int uv) {
			TString s = check_exp<TString> (uv < p.sizeupvalues, p.upvalues[uv].name);
			if (s == null) return "?";
			else return getsstr (s);
		}


		public static string findvararg (lua_State L, CallInfo ci, int n, ref int pos) {
			int nparam = clLvalue (L, ci.func).p.numparams;
			if (n >= ci.u.l.nbase - ci.func - nparam)
				return null;
			else {
				pos = ci.func + nparam + n;
				return "(*vararg)";
			}
		}


		public static string findlocal (lua_State L, CallInfo ci, int n, ref int pos) {
			string name = null;
			int sbase = 0;
			if (isLua (ci)) {
				if (n < 0)  /* access to vararg values? */
					return findvararg (L, ci, -n, ref pos);
				else {
					sbase = ci.u.l.nbase;
					name = luaF_getlocalname (ci_func (L, ci).p, n, currentpc (L, ci));
				}
			}
			else
				sbase = ci.func + 1;
			if (name == null) {  /* no 'standard' name? */
				int limit = (ci == L.ci ? L.top : ci.next.func);
				if (limit - sbase >= n && n > 0)  /* is 'n' inside 'ci' stack? */
					name = "(*temporary)";  /* generic name for any valid slot */
				else
					return null;  /* no name */
			}
			pos = sbase + (n - 1);
			return name;
		}


        public static void funcinfo (lua_Debug ar, Closure cl) {
            if (ldebug.noLuaClosure (cl)) {
                ar.source = "=[C]";
                ar.linedefined = -1;
                ar.lastlinedefined = -1;
                ar.what = "C";
            }
            else {
                Proto p = cl.l.p;
                ar.source = p.source == null ? getsstr (p.source) : "=?";
                ar.linedefined = p.linedefined;
                ar.lastlinedefined = p.lastlinedefined;
                ar.what = ar.linedefined == 0 ? "main" : "Lua";
            }
            luaO_chunkid (ref ar.short_src, ar.source, cc.LUA_IDSIZE);
        }


        public static void collectvalidlines (lua_State L, Closure f) {
            if (ldebug.noLuaClosure (f)) {
                setnilvalue (L, L.top);
                api_incr_top (L);
            }
            else {
                int[] lineinfo = f.l.p.lineinfo;
                TValue v = new TValue ();
                Table t = luaH_new (L);  /* new table to store active lines */
                sethvalue (L, L.top, t);  /* push it on stack */
                api_incr_top (L);
                setbvalue (v, 1);  /* boolean 'true' to be the value of all indices */
                for (int i = 0; i < f.l.p.sizelineinfo; i++)  /* for all lines with code */
                    luaH_setint (L, t, lineinfo[i], v);  /* table[line] = true */
            }
        }


        public static int auxgetinfo (lua_State L, string what, int index, lua_Debug ar, Closure f, CallInfo ci) {
            int status = 1;
            for (int i = index; i < what.Length; i++) {
                switch (what[i]) {
                    case 'S': {
                        funcinfo (ar, f);
                        break;
                    }
                    case 'l': {
                        ar.currentline = (ci != null && isLua (ci)) ? currentline (L, ci) : -1;
                        break;
                    }
                    case 'u': {
                        ar.nups = (byte)((f == null) ? 0 : f.c.nupvalues);
                        if (ldebug.noLuaClosure (f)) {
                            ar.isvararg = 1;
                            ar.nparams = 0;
                        }
                        else {
                            ar.isvararg = f.l.p.is_vararg;
                            ar.nparams = f.l.p.numparams;
                        }
                        break;
                    }
                    case 't': {
                        ar.istailcall = (byte)((ci != null) ? (ci.callstatus & CIST_TAIL) : 0);
                        break;
                    }
                    case 'n': {
                        /* calling function is a known Lua function? */
                        if (ci != null && ((ci.callstatus & CIST_TAIL) != 0) && isLua (ci.previous))
                            ar.namewhat = getfuncname (L, ci.previous, ref ar.name);
                        else
                            ar.namewhat = null;
                        if (ar.namewhat == null) {
                            ar.namewhat = "";  /* not found */
                            ar.name = null;
                        }
                        break;
                    }
                    case 'L': goto case 'f';
                    case 'f':  /* handled by lua_getinfo */
                        break;
                    default: {
                        status = 0;  /* invalid option */
                        break;
                    }
                }
            }
            return status;
        }



        /*
        ** {======================================================
        ** Symbolic Execution
        ** =======================================================
        */


        /*
        ** find a "name" for the RK value 'c'
        */
        public static void kname (Proto p, int pc, int c, ref string name) {
            if (ISK (c)) {  /* is 'c' a constant? */
                TValue kvalue = p.k[INDEXK (c)];
                if (ttisstring (kvalue)) {  /* literal constant? */
                    name = ssvalue (kvalue);  /* it is its own name */
                    return;
                }
                /* else no reasonable name found */
            }
            else {  /* 'c' is a register */
                string what = getobjname (p, pc, c, ref name);  /* search for 'c' */
                if (what != null && what[0] == 'c') {  /* found a constant name? */
                    return;  /* 'name' already filled */
                }
                /* else no reasonable name found */
            }
            name = "?";  /* no reasonable name found */
        }


        public static int filterpc (int pc, int jmptarget) {
            if (pc < jmptarget)  /* is code conditional (inside a jump)? */
                return -1;  /* cannot know who sets that register */
            else return pc;  /* current position sets that register */
        }


        /*
        ** try to find last instruction before 'lastpc' that modified register 'reg'
        */
        public static int findsetreg (Proto p, int lastpc, int reg) {
            int setreg = -1;  /* keep last instruction that changed 'reg' */
            int jmptarget = 0;
            for (int pc = 0; pc < lastpc; pc++) {
                uint i = p.code[pc];
                OpCode op = GET_OPCODE (i);
                int a = GETARG_A (i);
                switch (op) {
                    case OpCode.OP_LOADNIL: {
                        int b = GETARG_B (i);
                        if (a <= reg && reg <= a + b)  /* set registers from 'a' to 'a+b' */
                            setreg = filterpc (pc, jmptarget);
                        break;
                    }
                    case OpCode.OP_TFORCALL: {
                        if (reg >= a + 2)  /* affect all regs above its base */
                            setreg = filterpc (pc, jmptarget);
                        break;
                    }
                    case OpCode.OP_CALL: goto case OpCode.OP_TAILCALL;
                    case OpCode.OP_TAILCALL: {
                        if (reg >= a)  /* affect all registers above base */
                            setreg = filterpc (pc, jmptarget);
                        break;
                    }
                    case OpCode.OP_JMP: {
                        int b = GETARG_sBx (i);
                        int dest = pc + 1 + b;
                        /* jump is forward and do not skip 'lastpc'? */
                        if (pc < dest && dest <= lastpc) {
                            if (dest > jmptarget)
                                jmptarget = dest;  /* update 'jmptarget' */
                        }
                        break;
                    }
                    default: {
                        if (testAMode (op) && reg == a)  /* any instruction that set A */
                            setreg = filterpc (pc, jmptarget);
                        break;
                    }
                }
            }
            return setreg;
        }


        public static string getobjname (Proto p, int lastpc, int reg, ref string name) {
            name = luaF_getlocalname (p, reg + 1, lastpc);
            if (name != null)
                return "local";
            int pc = findsetreg (p, lastpc, reg);
            if (pc != -1) {
                uint i = p.code[pc];
                OpCode op = GET_OPCODE (i);
                switch (op) {
                    case OpCode.OP_MOVE: {
                        int b = GETARG_B (i);
                        if (b < GETARG_A (i))
                            return getobjname (p, pc, b, ref name);
                        break;
                    }
                    case OpCode.OP_GETTABUP: goto case OpCode.OP_GETTABLE;
                    case OpCode.OP_GETTABLE: {
                        int k = GETARG_C (i);
                        int t = GETARG_C (i);
                        string vn = (op == OpCode.OP_GETTABLE)
                                    ? luaF_getlocalname (p, t + 1, pc)
                                    : upvalname (p, t);
                        kname (p, pc, k, ref name);
                        return ((vn != null && vn == LUA_ENV) ? "global" : "field");
                    }
                    case OpCode.OP_GETUPVAL: {
                        name = upvalname (p, GETARG_B (i));
                        return "upvalue";
                    }
                    case OpCode.OP_LOADK: goto case OpCode.OP_LOADKX;
                    case OpCode.OP_LOADKX: {
                        int b = (op == OpCode.OP_LOADK) ? GETARG_Bx (i) : GETARG_Ax (p.code[pc + 1]);
                        if (ttisstring (p.k[b])) {
                            name = ssvalue (p.k[b]);
                            return "constant";
                        }
                        break;
                    }
                    case OpCode.OP_SELF: {
                        int k = GETARG_C (i);
                        kname (p, pc, k, ref name);
                        return "method";
                    }
                    default: break;
                }
            }
            return null;
        }


        public static string getfuncname (lua_State L, CallInfo ci, ref string name) {
            TMS tm = (TMS)0;  /* to avoid warnings */
            Proto p = ci_func (L, ci).p;  /* calling function */
            int pc = currentpc (L, ci);  /* calling instruction index */
            uint i = p.code[pc];  /* calling instruction */
            if ((ci.callstatus & CIST_HOOKED) != 0) {  /* was it called inside a hook? */
                name = "?";
                return "hook";
            }
            switch (GET_OPCODE (i)) {
                case OpCode.OP_CALL: goto case OpCode.OP_TAILCALL;
                case OpCode.OP_TAILCALL:  /* get function name */
                    return getobjname (p, pc, GETARG_A (i), ref name);
                case OpCode.OP_TFORCALL: {  /* for iterator */
                    name = "for iterator";
                    return "for iterator";
                }
                /* all other instructions can call only through metamethods */
                case OpCode.OP_SELF: goto case OpCode.OP_GETTABLE;
                case OpCode.OP_GETTABUP: goto case OpCode.OP_GETTABLE;
                case OpCode.OP_GETTABLE:
                    tm = TMS.TM_INDEX;
                    break;
                case OpCode.OP_SETTABUP: goto case OpCode.OP_SETTABLE;
                case OpCode.OP_SETTABLE:
                    tm = TMS.TM_NEWINDEX;
                    break;
                case OpCode.OP_ADD: goto case OpCode.OP_SHR;
                case OpCode.OP_SUB: goto case OpCode.OP_SHR;
                case OpCode.OP_MUL: goto case OpCode.OP_SHR;
                case OpCode.OP_MOD: goto case OpCode.OP_SHR;
                case OpCode.OP_POW: goto case OpCode.OP_SHR;
                case OpCode.OP_DIV: goto case OpCode.OP_SHR;
                case OpCode.OP_IDIV: goto case OpCode.OP_SHR;
                case OpCode.OP_BAND: goto case OpCode.OP_SHR;
                case OpCode.OP_BOR: goto case OpCode.OP_SHR;
                case OpCode.OP_BXOR: goto case OpCode.OP_SHR;
                case OpCode.OP_SHL: goto case OpCode.OP_SHR;
                case OpCode.OP_SHR: {
                    int offset = GET_OPCODE (i) - OpCode.OP_ADD;  /* ORDER OP */
                    tm = TMS.TM_ADD + offset;  /* ORDER TM */
                    break;
                }
                case OpCode.OP_UNM: tm = TMS.TM_UNM; break;
                case OpCode.OP_BNOT: tm = TMS.TM_BNOT; break;
                case OpCode.OP_LEN: tm = TMS.TM_LEN; break;
                case OpCode.OP_CONCAT: tm = TMS.TM_CONCAT; break;
                case OpCode.OP_EQ: tm = TMS.TM_EQ; break;
                case OpCode.OP_LT: tm = TMS.TM_LT; break;
                case OpCode.OP_LE: tm = TMS.TM_LE; break;
                default: lua_assert (false); break;  /* other instructions cannot call a function */
            }
            name = getsstr (G (L).tmname[(int)tm]);
            return "metamethod";
        }

        /* }====================================================== */



        /*
        ** The subtraction of two potentially unrelated pointers is
        ** not ISO C, but it should not crash a program; the subsequent
        ** checks are ISO C and ensure a correct result.
        */
        public static bool isinstatck (CallInfo ci, TValue o) {
            // TODO
            return false;
        }


        /*
        ** Checks whether value 'o' came from an upvalue. (That can only happen
        ** with instructions OP_GETTABUP/OP_SETTABUP, which operate directly on
        ** upvalues.)
        */
        public static string getupvalname (lua_State L, CallInfo ci, TValue o, ref string name) {
            LClosure c = ci_func (L, ci);
            for (int i = 0; i < c.nupvalues; i++) {
                if (c.upvals[i].v == o) {
                    name = upvalname (c.p, i);
                    return "upvalue";
                }
            }
            return null;
        }


        public static string varinfo (lua_State L, TValue o) {
            string name = null;
            CallInfo ci = L.ci;
            string kind = null;
            if (isLua (ci)) {
                kind = getupvalname (L, ci, o, ref name);
                if (kind == null && isinstatck (ci, o))
                    // TODO : reg
                    kind = getobjname (ci_func (L, ci).p, currentpc (L, ci), 0, ref name);
            }
            return (kind != null) ? luaO_pushfstring (L, " (%s '%s')", kind, name) : "";
        }


        public static void luaG_typeerror (lua_State L, TValue o, string op) {
            string t = objtypename (o);
            luaG_runerror (L, "attempt to %s a %s value%s", op, t, varinfo (L, o));
        }


        public static void luaG_concaterror (lua_State L, TValue p1, TValue p2) {
            if (ttisstring (p1) || cvt2str (p1)) p1 = p2;
            luaG_typeerror (L, p1, "concatenate");
        }


        public static void luaG_opinterror (lua_State L, TValue p1, TValue p2, string msg) {
            double temp = 0;
            if (tonumber (p1, ref temp) == false)  /* first operand is wrong? */
                p2 = p1;  /* now second is wrong */
            luaG_typeerror (L, p2, msg);
        }


        /*
        ** Error when both values are convertible to numbers, but not to integers
        */
        public static void luaG_tointerror (lua_State L, TValue p1, TValue p2) {
            long temp = 0;
            if (tointeger (p1, ref temp) == false)  /* first operand is wrong? */
                p2 = p1;  /* now second is wrong */
            luaG_runerror (L, "number%s has no integer representation", varinfo (L, p2));
        }


        public static void luaG_ordererror (lua_State L, TValue p1, TValue p2) {
            string t1 = objtypename (p1);
            string t2 = objtypename (p2);
            if (t1 == t2)
                luaG_runerror (L, "attempt to compare two %s values", t1);
            else
                luaG_runerror (L, "attempt to compare %s with %s", t1, t2);
        }


        public static void luaG_errormsg (lua_State L) {
            if (L.errfunc != 0) {  /* is there an error handling function? */
                int errfunc = restorestack (L, L.errfunc);
                setobjs2s (L, L.top, L.top - 1);  /* move argument */
                setobjs2s (L, L.top - 1, errfunc);  /* push function */
                L.top++;  /* assume EXTRA_STACK */
                luaD_call (L, L.top - 2, 1, 0);  /* call it */
            }
            luaD_throw (L, cc.LUA_ERRRUN);
        }


        public static void luaG_runerror (lua_State L, string format, params object[] args) {
            // TODO
        }


        public static void luaG_traceexec (lua_State L) {
            CallInfo ci = L.ci;
            byte mask = L.hookmask;
            bool counthook = ((mask & cc.LUA_MASKCOUNT) != 0 && L.hookcount == 0);
            if (counthook)
                resethookcount (L);  /* reset count */
            if ((ci.callstatus & CIST_HOOKYIELD) != 0) {  /* called hook last time? */
                ci.callstatus = (byte)(ci.callstatus & (~CIST_HOOKYIELD));  /* erase mark */
                return;  /* do not call hook again (VM yielded, so it did not move) */
            }
            if (counthook)
                luaD_hook (L, cc.LUA_HOOKCOUNT, -1);  /* call count hook */
            if ((mask & cc.LUA_MASKLINE) != 0) {
                Proto p = ci_func (L, ci).p;
                int npc = pcRel (ci.u.l.savedpc, p);
                int newline = getfuncline (p, npc);
                if (npc == 0 ||  /* call linehook when enter a new function, */
                    ci.u.l.savedpc <= L.oldpc ||  /* when jump back (loop), or when */
                    newline != getfuncline (p, pcRel (L.oldpc, p)))  /* enter a new line */
                    luaD_hook (L, cc.LUA_HOOKLINE, newline);  /* call line hook */
            }
            L.oldpc = ci.u.l.savedpc;
            if (L.status == cc.LUA_YIELD) {  /* did hook yield? */
                if (counthook)
                    L.hookcount = 1;  /* undo decrement to zero */
                ci.u.l.savedpc--;  /* undo increment (resume will increment it again) */
                ci.callstatus |= CIST_HOOKYIELD;  /* mark that it yielded */
                ci.func = L.top - 1;  /* protect stack below results */
                luaD_throw (L, cc.LUA_YIELD);
            }
        }
    }



	
	
	public static partial class lua530 {

		/*
		** this function can be called asynchronous (e.g. during a signal)
		*/
		public static void lua_sethook (lua_State L, lua_Hook func, int mask, int count) {
			if (func == null || mask == 0) {
				mask = 0;
				func = null;
			}
			if (imp.isLua (L.ci))
				L.oldpc = L.ci.u.l.savedpc;
			L.hook = func;
			L.basehookcount = count;
			imp.resethookcount (L);
			L.hookmask = (byte)mask;
		}


		public static lua_Hook lua_gethook (lua_State L) {
			return L.hook;
		}


		public static int lua_gethookmask (lua_State L) {
			return L.hookmask;
		}
		
		
		public static int lua_gethookcount (lua_State L) {
			return L.basehookcount;
		}


		public static int lua_getstack (lua_State L, int level, lua_Debug ar) {
			int status = 0;
			if (level < 0) return 0;  /* invalid (negative) level */
			lua_lock (L);
			CallInfo ci;
			for (ci = L.ci; level > 0 && ci != L.base_ci; ci = ci.previous)
				level--;
			if (level == 0 && ci != L.base_ci) {  /* level found? */
				status = 1;
				ar.i_ci = ci;
			}
			else status = 0;  /* no such level */
			lua_unlock (L);
			return status;
		}


		public static string lua_getlocal (lua_State L, lua_Debug ar, int n) {
			lua_lock (L);
			string name = null;
            if (ar == null) {  /* information about non-active function? */
                if (imp.isLfunction (L, L.top - 1) == false)  /* not a Lua function? */
					name = null;
                else  /* consider live variables at function start (parameters) */
					name = imp.luaF_getlocalname (imp.clLvalue (L, L.top - 1).p, n, 0);
			}
            else {  /* active function; get information through 'ar' */
                int pos = 0;  /* to avoid warnings */
				name = imp.findlocal (L, ar.i_ci, n, ref pos);
				if (name != null) {
					imp.setobj2s (L, L.top, pos);
					imp.api_incr_top (L);
				}
			}
			lua_unlock (L);
			return name;
		}


		public static string lua_setlocal (lua_State L, lua_Debug ar, int n) {
            int pos = 0;  /* to avoid warnings */
			string name = imp.findlocal (L, ar.i_ci, n, ref pos);
			lua_lock (L);
			if (name != null) {
				imp.setobj2s (L, pos, L.top - 1);
                L.top--;  /* pop value */
			}
			lua_unlock (L);
			return name;
		}


        public static int lua_getinfo (lua_State L, string what, lua_Debug ar) {
            lua_lock (L);
            CallInfo ci;
            int func;
            int i = 0;
            if (what[i] == '>') {
                ci = null;
                func = L.top - 1;
                imp.api_check (imp.ttisfunction (L, func), "function expected");
                i++;
                L.top--;
            }
            else {
                ci = ar.i_ci;
                func = ci.func;
                imp.lua_assert (imp.ttisfunction (L, ci.func));
            }
            Closure cl = imp.ttisclosure (L, func) ? imp.clvalue (L, func) : null;
            int status = imp.auxgetinfo (L, what, i, ar, cl, ci);
            if (what.IndexOf ('L') >= 0)
                imp.collectvalidlines (L, cl);
            lua_unlock (L);
            return status;
        }



















	}
}
