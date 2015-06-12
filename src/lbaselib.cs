using System;

using cc = cclua.lua530;

using lua_State = cclua.lua530.lua_State;
using luaL_Reg = cclua.lua530.luaL_Reg;

namespace cclua {
	
	public static partial class imp {

        public static class lbase {

            public static string SPACECHARS = " \f\n\r\t\v";

            public static string b_str2int (string s, int nbase, ref long pn) {
                // TODO
                return s;
            }


            public static string[] gc_opts = {
                "stop", "restart", "collect", "count", 
                "step", "setpause", "setstepmul", "isrunning", null };

            public static int[] gc_optsnum = {
                cc.LUA_GCSTOP, cc.LUA_GCRESTART, cc.LUA_GCCOLLECT, cc.LUA_GCCOUNT, 
                cc.LUA_GCSTEP, cc.LUA_GCSETPAUSE, cc.LUA_GCSETSTEPMUL, cc.LUA_GCISRUNNING };


            public static int pairsmeta (lua_State L, string method, bool iszero, cc.lua_CFunction iter) {
                if (cc.luaL_getmetafield (L, 1, method) == cc.LUA_TNIL) {  /* no metamethod? */
                    cc.luaL_checktype (L, 1, cc.LUA_TTABLE);  /* argument must be a table */
                    cc.lua_pushcfunction (L, iter);  /* will return generator, */
                    cc.lua_pushvalue (L, 1);  /* state, */
                    if (iszero) cc.lua_pushinteger (L, 0);  /* and initial value */
                    else cc.lua_pushnil (L);
                }
                else {
                    cc.lua_pushvalue (L, 1);  /* argument 'self' to metamethod */
                    cc.lua_call (L, 1, 3);  /* get 3 values from metamethod */
                }
                return 3;
            }


            /*
            ** Traversal function for 'ipairs' for raw tables
            */
            public static int ipairsaux_raw (lua_State L) {
                long i = cc.luaL_checkinteger (L, 2) + 1;
                cc.luaL_checktype (L, 1, cc.LUA_TTABLE);
                cc.lua_pushinteger (L, i);
                return (cc.lua_rawgeti (L, 1, i) == cc.LUA_TNIL) ? 1 : 2;
            }


            /*
            ** Traversal function for 'ipairs' for tables with metamethods
            */
            public static int ipairaux (lua_State L) {
                long i = cc.luaL_checkinteger (L, 2) + 1;
                cc.lua_pushinteger (L, i);
                return (cc.lua_geti (L, 1, i) == cc.LUA_TNIL) ? 1 : 2;
            }


            public static int load_aux (lua_State L, int status, int envidx) {
                if (status == cc.LUA_OK) {
                    if (envidx != 0) {  /* 'env' parameter? */
                        cc.lua_pushvalue (L, envidx);  /* environment for loaded function */
                        if (cc.lua_setupvalue (L, -2, 1) == null)  /* set it as 1st upvalue */
                            cc.lua_pop (L, 1);  /* remove 'env' if not used by previous call */
                    }
                    return 1;
                }
                else {  /* error (message is on top of the stack) */
                    cc.lua_pushnil (L);
                    cc.lua_insert (L, -2);  /* put before error message */
                    return 2;  /* return nil plus error message */
                }
            }



            /*
            ** {======================================================
            ** Generic Read function
            ** =======================================================
            */


            /*
            ** reserved slot, above all arguments, to hold a copy of the returned
            ** string to avoid it being collected while parsed. 'load' has four
            ** optional arguments (chunk, source name, mode, and environment).
            */
            public const int RESERVEDSLOT = 5;


            /*
            ** Reader for generic 'load' function: 'lua_load' uses the
            ** stack for internal stuff, so the reader cannot change the
            ** stack top. Instead, it keeps its resulting string in a
            ** reserved slot inside the stack.
            */
            public static byte[] generic_reader (lua_State L, object ud, ref int size) {
                cc.luaL_checkstack (L, 2, "too many nested functions");
                cc.lua_pushvalue (L, 1);  /* get function */
                cc.lua_call (L, 0, 1);  /* call it */
                if (cc.lua_isnil (L, -1) != 0) {
                    cc.lua_pop (L, 1);  /* pop result */
                    size = 0;
                    return null;
                }
                else if (cc.lua_isstring (L, -1) == 0)
                    cc.luaL_error (L, "reader function must return a string");
                cc.lua_replace (L, RESERVEDSLOT);  /* save string in reserved slot */
                return str2byte (cc.lua_tolstring (L, RESERVEDSLOT, ref size));
            }

            /* }====================================================== */


            public static int dofilecont (lua_State L, int d1, long d2) {
                return cc.lua_gettop (L) - 1;
            }


            /*
            ** Continuation function for 'pcall' and 'xpcall'. Both functions
            ** already pushed a 'true' before doing the call, so in case of success
            ** 'finishpcall' only has to return everything in the stack minus
            ** 'extra' values (where 'extra' is exactly the number of items to be
            ** ignored).
            */
            public static int finishpcall (lua_State L, int status, long extra) {
                if (status != cc.LUA_OK && status != cc.LUA_YIELD) {  /* error? */
                    cc.lua_pushboolean (L, 0);  /* first result (false) */
                    cc.lua_pushvalue (L, -2);  /* error message */
                    return 2;  /* return false, msg */
                }
                else
                    return (cc.lua_gettop (L) - (int)extra);  /* return all results */
            }
        }




        public static int luaB_print (lua_State L) {
            int n = cc.lua_gettop (L);
            cc.lua_getglobal (L, "tostring");
            for (int i = 1; i <= n; i++) {
                cc.lua_pushvalue (L, -1);
                cc.lua_pushvalue (L, i);
                cc.lua_call (L, 1, 1);
                int l = 0;
                string s = cc.lua_tolstring (L, -1, ref l);
                if (s == null)
                    return cc.luaL_error (L, "'tostring' must return a string to 'print'");
                if (i > 1) cc.lua_writestring ("\t");
                cc.lua_writestring (s);
                cc.lua_pop (L, 1);
            }
            cc.lua_writeline ();
            return 0;
        }


        public static int luaB_tonumber (lua_State L) {
            if (cc.lua_isnoneornil (L, 2) != 0) {  /* standard conversion? */
                cc.luaL_checkany (L, 1);
                if (cc.lua_type (L, 1) == cc.LUA_TNUMBER) {  /* already a number? */
                    cc.lua_settop (L, 1);  /* yes; return it */
                    return 1;
                }
                else {
                    int l = 0;
                    string s = cc.lua_tolstring (L, 1, ref l);
                    if (s != null && cc.lua_stringtonumber (L, s) == l + 1)
                        return 1;  /* successful conversion to number */
                    /* else not a number */
                }
            }
            else {
                long nbase = cc.luaL_checkinteger (L, 2);
                cc.luaL_checktype (L, 1, cc.LUA_TSTRING);  /* before 'luaL_checklstring'! */
                int l = 0;
                string s = cc.luaL_checklstring (L, 1, ref l);
                cc.luaL_argcheck (L, 2 <= nbase && nbase <= 36, 2, "base out of range");
                long n = 0;  /* to avoid warnings */
                if (lbase.b_str2int (s, (int)nbase, ref n) == s + l) {
                    cc.lua_pushinteger (L, n);
                    return 1;
                }  /* else not a number */
            }  /* else not a number */
            cc.lua_pushnil (L);  /* not a number */
            return 1;
        }


        public static int luaB_error (lua_State L) {
            int level = (int)cc.luaL_optinteger (L, 2, 1);
            cc.lua_settop (L, 1);
            if (cc.lua_isstring (L, 1) != 0 && level > 0) {  /* add extra information? */
                cc.luaL_where (L, level);
                cc.lua_pushvalue (L, 1);
                cc.lua_concat (L, 2);
            }
            return cc.lua_error (L);
        }


        public static int luaB_getmetatable (lua_State L) {
            cc.luaL_checkany (L, 1);
            if (cc.lua_getmetatable (L, 1) == 0) {
                cc.lua_pushnil (L);
                return 1;  /* no metatable */
            }
            cc.luaL_getmetafield (L, 1, "__metatable");
            return 1;  /* returns either __metatable field (if present) or metatable */
        }


        public static int luaB_setmetatable (lua_State L) {
            int t = cc.lua_type (L, 2);
            cc.luaL_checktype (L, 1, cc.LUA_TTABLE);
            cc.luaL_argcheck (L, t == cc.LUA_TNIL || t == cc.LUA_TTABLE, 2, "nil or table expected");
            if (cc.luaL_getmetafield (L, 1, "__metatable") != cc.LUA_TNIL)
                return cc.luaL_error (L, "cannot change a protected metatable");
            cc.lua_settop (L, 2);
            cc.lua_setmetatable (L, 1);
            return 1;
        }


        public static int luaB_rawequal (lua_State L) {
            cc.luaL_checkany (L, 1);
            cc.luaL_checkany (L, 2);
            cc.lua_pushboolean (L, cc.lua_rawequal (L, 1, 2));
            return 1;
        }


        public static int luaB_rawlen (lua_State L) {
            int t = cc.lua_type (L, 1);
            cc.luaL_argcheck (L, t == cc.LUA_TTABLE || t == cc.LUA_TSTRING, 1, "table or string expected");
            cc.lua_pushinteger (L, cc.lua_rawlen (L, 1));
            return 1;
        }


        public static int luaB_rawget (lua_State L) {
            cc.luaL_checktype (L, 1, cc.LUA_TTABLE);
            cc.luaL_checkany (L, 2);
            cc.lua_settop (L, 2);
            cc.lua_rawget (L, 1);
            return 1;
        }

        public static int luaB_rawset (lua_State L) {
            cc.luaL_checktype (L, 1, cc.LUA_TTABLE);
            cc.luaL_checkany (L, 2);
            cc.luaL_checkany (L, 3);
            cc.lua_settop (L, 3);
            cc.lua_rawset (L, 1);
            return 1;
        }


        public static int luaB_collectgarbage (lua_State L) {
            int o = lbase.gc_optsnum[cc.luaL_checkoption (L, 1, "collect", lbase.gc_opts)];
            int ex = (int)cc.luaL_optinteger (L, 2, 0);
            int res = cc.lua_gc (L, o, ex);
            switch (o) {
                case cc.LUA_GCCOUNT: {
                    int b = cc.lua_gc (L, cc.LUA_GCCOUNTB, 0);
                    cc.lua_pushnumber (L, (double)res + (double)b / 1024);
                    return 1;
                }
                case cc.LUA_GCSTEP: goto case cc.LUA_GCISRUNNING;
                case cc.LUA_GCISRUNNING: {
                    cc.lua_pushboolean (L, res);
                    return 1;
                }
                default: {
                    cc.lua_pushinteger (L, res);
                    return 1;
                }
            }
        }


        /*
        ** This function has all type names as upvalues, to maximize performance.
        */
        public static int luaB_type (lua_State L) {
            cc.luaL_checkany (L, 1);
            cc.lua_pushvalue (L, cc.lua_upvalueindex (cc.lua_type (L, 1) + 1));
            return 1;
        }


        public static int luaB_next (lua_State L) {
            cc.luaL_checktype (L, 1, cc.LUA_TTABLE);
            cc.lua_settop (L, 2);  /* create a 2nd argument if there isn't one */
            if (cc.lua_next (L, 1) != 0)
                return 2;
            else {
                cc.lua_pushnil (L);
                return 1;
            }
        }


        public static int luaB_pairs (lua_State L) {
            return lbase.pairsmeta (L, "__pairs", false, luaB_next);
        }


        /*
        ** This function will use either 'ipairsaux' or 'ipairsaux_raw' to
        ** traverse a table, depending on whether the table has metamethods
        ** that can affect the traversal.
        */
        public static int luaB_ipairs (lua_State L) {
            cc.lua_CFunction iter = (cc.luaL_getmetafield (L, 1, "__index") != cc.LUA_TNIL) ? (cc.lua_CFunction)lbase.ipairaux : (cc.lua_CFunction)lbase.ipairsaux_raw;
            cc.luaL_checkany (L, 1);
            cc.lua_pushcfunction (L, iter);  /* iteration function */
            cc.lua_pushvalue (L, 1);  /* state */
            cc.lua_pushinteger (L, 0);  /* initial value */
            return 3;
        }


        public static int luaB_loadfile (lua_State L) {
            string fname = cc.luaL_optstring (L, 1, null);
            string mode = cc.luaL_optstring (L, 2, null);
            int env = (cc.lua_isnone (L, 3) == 0 ? 3 : 0);  /* 'env' index or 0 if no 'env' */
            int status = cc.luaL_loadfilex (L, fname, mode);
            return lbase.load_aux (L, status, env);
        }


        public static int luaB_load (lua_State L) {
            int l = 0;
            string s = cc.lua_tolstring (L, 1, ref l);
            string mode = cc.luaL_optstring (L, 3, "bt");
            int env = (cc.lua_isnone (L, 4) == 0 ? 4 : 0);
            int status = 0;
            if (s != null) {
                string chunkname = cc.luaL_optstring (L, 2, s);
                byte[] buff = str2byte (s);
                status = cc.luaL_loadbufferx (L, buff, buff.Length, chunkname, mode);
            }
            else {
                string chunkname = cc.luaL_optstring (L, 2, "=load");
                cc.luaL_checktype (L, 1, cc.LUA_TFUNCTION);
                cc.lua_settop (L, lbase.RESERVEDSLOT);
                status = cc.lua_load (L, (cc.lua_Reader)lbase.generic_reader, null, chunkname, mode);
            }
            return lbase.load_aux (L, status, env);
        }


        public static int luaB_dofile (lua_State L) {
            string fname = cc.luaL_optstring (L, 1, null);
            cc.lua_settop (L, 1);
            if (cc.luaL_loadfile (L, fname) != cc.LUA_OK)
                return cc.lua_error (L);
            cc.lua_callk (L, 0, cc.LUA_MULTRET, 0, lbase.dofilecont);
            return lbase.dofilecont (L, 0, 0);
        }


        public static int luaB_assert (lua_State L) {
            if (cc.lua_toboolean (L, 1) != 0)  /* condition is true? */
                return cc.lua_gettop (L);  /* return all arguments */
            else {  /* error */
                cc.luaL_checkany (L, 1);  /* there must be a condition */
                cc.lua_remove (L, 1);  /* remove it */
                cc.lua_pushliteral (L, "assertion failed!");  /* default message */
                cc.lua_settop (L, 1);  /* leave only message (default if no other one) */
                return luaB_error (L);  /* call 'error' */
            }
        }


        public static int luaB_select (lua_State L) {
            int n = cc.lua_gettop (L);
            if (cc.lua_type (L, 1) == cc.LUA_TSTRING && cc.lua_tostring (L, 1)[0] == '#') {
                cc.lua_pushinteger (L, n - 1);
                return 1;
            }
            else {
                long i = cc.luaL_checkinteger (L, 1);
                if (i < 0) i = n + 1;
                else if (i > n) i = n;
                cc.luaL_argcheck (L, 1 <= i, 1, "index out of range");
                return (n - (int)i);
            }
        }


        public static int luaB_pcall (lua_State L) {
            cc.luaL_checkany (L, 1);
            cc.lua_pushboolean (L, 1);
            cc.lua_insert (L, 1);
            int status = cc.lua_pcallk (L, cc.lua_gettop (L) - 2, cc.LUA_MULTRET, 0, 0, lbase.finishpcall);
            return lbase.finishpcall (L, status, 0);
        }


        /*
        ** Do a protected call with error handling. After 'lua_rotate', the
        ** stack will have <f, err, true, f, [args...]>; so, the function passes
        ** 2 to 'finishpcall' to skip the 2 first values when returning results.
        */
        public static int luaB_xpcall (lua_State L) {
            int n = cc.lua_gettop (L);
            cc.luaL_checktype (L, 2, cc.LUA_TFUNCTION);
            cc.lua_pushboolean (L, 1);
            cc.lua_pushvalue (L, 1);
            cc.lua_rotate (L, 3, 2);
            int status = cc.lua_pcallk (L, n - 2, cc.LUA_MULTRET, 2, 2, lbase.finishpcall);
            return lbase.finishpcall (L, status, 2);
        }


        public static int luaB_tostring (lua_State L) {
            cc.luaL_checkany (L, 1);
            int l = 0;
            cc.luaL_tolstring (L, 1, ref l);
            return 1;
        }


		public static luaL_Reg[] base_funcs = {
			new luaL_Reg ("assert", luaB_assert),
			new luaL_Reg ("collectgarbage", luaB_collectgarbage),
			new luaL_Reg ("dofile", luaB_dofile),
			new luaL_Reg ("error", luaB_error),
			new luaL_Reg ("getmetatable", luaB_getmetatable),
			new luaL_Reg ("ipairs", luaB_ipairs),
			new luaL_Reg ("loadfile", luaB_loadfile),
			new luaL_Reg ("load", luaB_load),
			new luaL_Reg ("next", luaB_next),
			new luaL_Reg ("pairs", luaB_pairs),
			new luaL_Reg ("pcall", luaB_pcall),
			new luaL_Reg ("print", luaB_print),
			new luaL_Reg ("rawequal", luaB_rawequal),
			new luaL_Reg ("rawlen", luaB_rawlen),
			new luaL_Reg ("rawget", luaB_rawget),
			new luaL_Reg ("rawset", luaB_rawset),
			new luaL_Reg ("select", luaB_select),
			new luaL_Reg ("setmetatable", luaB_setmetatable),
			new luaL_Reg ("tonumber", luaB_tonumber),
			new luaL_Reg ("tostring", luaB_tostring),
			new luaL_Reg ("xpcall", luaB_xpcall),
            /* placeholders */
			new luaL_Reg ("type", null),
			new luaL_Reg ("_G", null),
			new luaL_Reg ("_VERSION", null),
			new luaL_Reg (null, null),
		};
	}
	
	public static partial class mod {

		public static int luaopen_base (lua_State L) {
			/* open lib into global table */
			cc.lua_pushglobaltable (L);
			cc.luaL_setfuncs (L, imp.base_funcs, 0);
            /* set global _G */
            cc.lua_pushvalue (L, -1);
            cc.lua_setfield (L, -2, "_G");
            /* set global _VERSION */
            cc.lua_pushliteral (L, cc.LUA_VERSION);
            cc.lua_setfield (L, -2, "_VERSION");
            /* set function 'type' with proper upvalues */
            for (int i = 0; i < cc.LUA_NUMTAGS; i++)  /* push all type names as upvalues */
                cc.lua_pushstring (L, cc.lua_typename (L, i));
            cc.lua_pushcclosure (L, imp.luaB_type, cc.LUA_NUMTAGS);
            cc.lua_setfield (L, -2, "type");
			return 1;
		}
	}
}
