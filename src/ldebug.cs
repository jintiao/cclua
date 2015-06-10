using System;

using lua_State = cclua.lua530.lua_State;
using CallInfo = cclua.imp.CallInfo;

namespace cclua {

	public static partial class imp {

		private static class ldebug {

			public static bool noLuaClosure (Closure f) { return (f == null || f.c.tt == LUA_TCCL); }
		}

		public static int pcRel (int pc, Proto p) { return (pc - p.code - 1); }

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


		public static byte[] upvalname (Proto p, int uv) {
			TString s = check_exp<TString> (uv < p.sizeupvalues, p.upvalues[uv].name);
			if (s == null) return "?";
			else return getstr (s);
		}


		public static string findvararg (CallInfo ci, int n, ref int pos) {
			int nparam = clLvalue (ci.func).p.numparams;
			if (n >= ci.u.l.fbase - ci.func - nparam)
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
					return findvararg (ci, -n, ref pos);
				else {
					sbase = ci.u.l.fbase;
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


















        public static void luaG_errormsg (lua_State L) {
            if (L.errfunc != 0) {  /* is there an error handling function? */
                int errfunc = restorestack (L, L.errfunc);
                setobjs2s (L, L.top, L.top - 1);  /* move argument */
                setobjs2s (L, L.top - 1, errfunc);  /* push function */
                L.top++;  /* assume EXTRA_STACK */
                luaD_call (L, L.top - 2, 1, 0);  /* call it */
            }
            luaD_throw (L, lua530.LUA_ERRRUN);
        }


        public static void luaG_runerror (lua_State L, string format, params object[] args) {
            // TODO
        }


        public static void luaG_typeerror (lua_State L, TValue o, string op) {
            // TODO
        }


        public static void luaG_concaterror (lua_State L, TValue p1, TValue p2) {
            // TODO
        }


        public static void luaG_opinterror (lua_State L, TValue p1, TValue p2, string msg) {
            // TODO
        }

        public static void luaG_tointerror (lua_State L, TValue p1, TValue p2) {
        }


        public static void luaG_ordererror (lua_State L, TValue p1, TValue p2) {
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
			imp.lua_lock (L);
			CallInfo ci;
			for (ci = L.ci; level > 0 && ci != L.base_ci; ci = ci.previous)
				level--;
			if (level == 0 && ci != L.base_ci) {  /* level found? */
				status = 1;
				ar.i_ci = ci;
			}
			else status = 0;  /* no such level */
			imp.lua_unlock (L);
			return status;
		}


		public static string lua_getlocal (lua_State L, lua_Debug ar, int n) {
			imp.lua_lock (L);
			string name = null;
			if (ar == null) {
				if (imp.isLfunction (L, L.top - 1) == false)
					name = null;
				else
					name = imp.luaF_getlocalname (imp.clLvalue (L, L.top - 1).p, n, 0);
			}
			else {
				int pos = 0;
				name = imp.findlocal (L, ar.i_ci, n, ref pos);
				if (name != null) {
					imp.setobj2s (L, L.top, pos);
					imp.api_incr_top (L);
				}
			}
			imp.lua_unlock (L);
			return name;
		}


		public static string lua_setlocal (lua_State L, lua_Debug ar, int n) {
			int pos = 0;
			string name = imp.findlocal (L, ar.i_ci, n, ref pos);
			imp.lua_lock (L);
			if (name != null) {
				imp.setobj2s (L, pos, L.top - 1);
				L.top--;
			}
			imp.lua_unlock (L);
			return name;
		}



















	}
}
