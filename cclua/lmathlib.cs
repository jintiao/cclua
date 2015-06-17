using System;

using cc = cclua.lua530;

using lua_State = cclua.lua530.lua_State;
using luaL_Reg = cclua.lua530.luaL_Reg;

namespace cclua {
	
	public static partial class imp {
		
		public static class lmath {

            public static Random random = new Random ();
            public static double l_rand () { return random.NextDouble (); }
            public static void l_srand (int x) { random = new Random (x); }


            public static void pushnumint (lua_State L, double d) {
                long n = 0;
                if (cc.lua_numbertointeger (d, ref n))  /* does 'd' fit in an integer? */
                    cc.lua_pushinteger (L, n);  /* result is integer */
                else
                    cc.lua_pushnumber (L, d);  /* result is float */
            }
		}

        public static double PI = 3.141592653589793238462643383279502884;
        public static double HUGE_VAL = Double.MaxValue;


        public static int math_abs (lua_State L) {
            if (cc.lua_isinteger (L, 1) != 0) {
                cc.lua_pushinteger (L, Math.Abs (cc.lua_tointeger (L, 1)));
            }
            else
                cc.lua_pushnumber (L, Math.Abs (cc.luaL_checknumber (L, 1)));
            return 1;
        }


        public static int math_sin (lua_State L) {
            cc.lua_pushnumber (L, Math.Sin (cc.luaL_checknumber (L, 1)));
            return 1;
        }


        public static int math_cos (lua_State L) {
            cc.lua_pushnumber (L, Math.Cos (cc.luaL_checknumber (L, 1)));
            return 1;
        }


        public static int math_tan (lua_State L) {
            cc.lua_pushnumber (L, Math.Tan (cc.luaL_checknumber (L, 1)));
            return 1;
        }


        public static int math_asin (lua_State L) {
            cc.lua_pushnumber (L, Math.Asin (cc.luaL_checknumber (L, 1)));
            return 1;
        }


        public static int math_acos (lua_State L) {
            cc.lua_pushnumber (L, Math.Acos (cc.luaL_checknumber (L, 1)));
            return 1;
        }


        public static int math_atan (lua_State L) {
            cc.lua_pushnumber (L, Math.Atan (cc.luaL_checknumber (L, 1)));
            return 1;
        }


        public static int math_toint (lua_State L) {
            int valid = 0;
            long n = cc.lua_tointegerx (L, 1, ref valid);
            if (valid != 0)
                cc.lua_pushinteger (L, n);
            else {
                cc.luaL_checkany (L, 1);
                cc.lua_pushnil (L);  /* value is not convertible to integer */
            }
            return 1;
        }


        public static int math_floor (lua_State L) {
            if (cc.lua_isinteger (L, 1) != 0) {
                cc.lua_settop (L, 1);  /* integer is its own floor */
            }
            else {
                double d = Math.Floor (cc.luaL_checknumber (L, 1));
                lmath.pushnumint (L, d);
            }
            return 1;
        }


        public static int math_ceil (lua_State L) {
            if (cc.lua_isinteger (L, 1) != 0) {
                cc.lua_settop (L, 1);  /* integer is its own ceil */
            }
            else {
                double d = Math.Ceiling (cc.luaL_checknumber (L, 1));
                lmath.pushnumint (L, d);
            }
            return 1;
        }


        public static int math_fmod (lua_State L) {
            if (cc.lua_isinteger (L, 1) != 0 && cc.lua_isinteger (L, 2) != 0) {
                long d = cc.lua_tointeger (L, 2);
                if (d == -1 || d == 0) {
                    cc.luaL_argcheck (L, d != 0, 2, "zero");
                    cc.lua_pushinteger (L, 0);
                }
                else
                    cc.lua_pushinteger (L, cc.lua_tointeger (L, 1) % d);
            }
            else {
                cc.lua_pushnumber (L, cc.luaL_checknumber (L, 1) % cc.luaL_checknumber (L, 2));
            }
            return 1;
        }


        /*
        ** next function does not use 'modf', avoiding problems with 'double*'
        ** (which is not compatible with 'float*') when lua_Number is not
        ** 'double'.
        */
        public static int math_modf (lua_State L) {
            if (cc.lua_isinteger (L, 1) != 0) {
                cc.lua_settop (L, 1);  /* number is its own integer part */
                cc.lua_pushnumber (L, 0);  /* no fractional part */
            }
            else {
                double n = cc.luaL_checknumber (L, 1);
                /* integer part (rounds toward zero) */
                double ip = (n < 0) ? Math.Ceiling (n) : Math.Floor (n);
                lmath.pushnumint (L, ip);
                /* fractional part (test needed for inf/-inf) */
                cc.lua_pushnumber (L, (n == ip) ? 0 : (n - ip));
            }
            return 2;
        }


        public static int math_sqrt (lua_State L) {
            cc.lua_pushnumber (L, Math.Sqrt (cc.luaL_checknumber (L, 1)));
            return 1;
        }


        public static int math_ult (lua_State L) {
            long a = cc.luaL_checkinteger (L, 1);
            long b = cc.luaL_checkinteger (L, 2);
            cc.lua_pushboolean (L, ((ulong)a < (ulong)b) ? 1 : 0);
            return 1;
        }


        public static int math_log (lua_State L) {
            double x = cc.luaL_checknumber (L, 1);
            double res = 0;
            if (cc.lua_isnoneornil (L, 2) != 0)
                res = Math.Log (x);
            else {
                double nbase = cc.luaL_checknumber (L, 2);
                if (nbase == 10) res = Math.Log10 (x);
                else res = Math.Log (x) / Math.Log (nbase);
            }
            cc.lua_pushnumber (L, res);
            return 1;
        }


        public static int math_exp (lua_State L) {
            cc.lua_pushnumber (L, Math.Exp (cc.luaL_checknumber (L, 1)));
            return 1;
        }


        public static int math_deg (lua_State L) {
            cc.lua_pushnumber (L, cc.luaL_checknumber (L, 1) * 180 / PI);
            return 1;
        }


        public static int math_rad (lua_State L) {
            cc.lua_pushnumber (L, cc.luaL_checknumber (L, 1) * PI / 180);
            return 1;
        }


        public static int math_min (lua_State L) {
            int n = cc.lua_gettop (L);  /* number of arguments */
            int imin = 1;  /* index of current minimum value */
            cc.luaL_argcheck (L, n >= 1, 1, "value expected");
            for (int i = 2; i <= n; i++) {
                if (cc.lua_compare (L, i, imin, cc.LUA_OPLT) != 0)
                    imin = i;
            }
            cc.lua_pushvalue (L, imin);
            return 1;
        }


        public static int math_max (lua_State L) {
            int n = cc.lua_gettop (L);  /* number of arguments */
            int imax = 1;  /* index of current maximum value */
            cc.luaL_argcheck (L, n >= 1, 1, "value expected");
            for (int i = 2; i <= n; i++) {
                if (cc.lua_compare (L, i, imax, cc.LUA_OPLT) != 0)
                    imax = i;
            }
            cc.lua_pushvalue (L, imax);
            return 1;
        }


        /*
        ** This function uses 'double' (instead of 'lua_Number') to ensure that
        ** all bits from 'l_rand' can be represented, and that 'RANDMAX + 1.0'
        ** will keep full precision (ensuring that 'r' is always less than 1.0.)
        */
        public static int math_random (lua_State L) {
            long low = 0;
            long up = 0;
            double r = lmath.l_rand ();
            switch (cc.lua_gettop (L)) {  /* check number of arguments */
                case 0: {  /* no arguments */
                    cc.lua_pushnumber (L, r);  /* Number between 0 and 1 */
                    return 1;
                }
                case 1: {  /* only upper limit */
                    low = 1;
                    up = cc.luaL_checkinteger (L, 1);
                    break;
                }
                case 2: {  /* lower and upper limits */
                    low = cc.luaL_checkinteger (L, 1);
                    up = cc.luaL_checkinteger (L, 2);
                    break;
                }
                default: return cc.luaL_error (L, "wrong number of arguments");
            }
            /* random integer in the interval [low, up] */
            cc.luaL_argcheck (L, low <= up, 1, "interval is empty");
            cc.luaL_argcheck (L, low >= 0 || up <= cc.LUA_MAXINTEGER + low, 1, "interval too large");
            r *= (up - low) + 1.0;
            cc.lua_pushinteger (L, (long)r + low);
            return 1;
        }


        public static int math_randomseed (lua_State L) {
            lmath.l_srand ((int)cc.luaL_checknumber (L, 1));
            lmath.l_rand ();  /* discard first value to avoid undesirable correlations */
            return 1;
        }


        public static int math_type (lua_State L) {
            if (cc.lua_type (L, 1) == cc.LUA_TNUMBER) {
                if (cc.lua_isinteger (L, 1) != 0)
                    cc.lua_pushliteral (L, "integer");
                else
                    cc.lua_pushliteral (L, "float");
            }
            else {
                cc.luaL_checkany (L, 1);
                cc.lua_pushnil (L);
            }
            return 1;
        }








        public static luaL_Reg[] math_funcs = {
			new luaL_Reg ("abs", math_abs),
			new luaL_Reg ("acos", math_acos),
			new luaL_Reg ("asin", math_asin),
			new luaL_Reg ("atan", math_atan),
			new luaL_Reg ("ceil", math_ceil),
			new luaL_Reg ("cos", math_cos),
			new luaL_Reg ("deg", math_deg),
			new luaL_Reg ("exp", math_exp),
			new luaL_Reg ("tointeger", math_toint),
			new luaL_Reg ("floor", math_floor),
			new luaL_Reg ("fmod", math_fmod),
			new luaL_Reg ("ult", math_ult),
			new luaL_Reg ("log", math_log),
			new luaL_Reg ("max", math_max),
			new luaL_Reg ("min", math_min),
			new luaL_Reg ("modf", math_modf),
			new luaL_Reg ("rad", math_rad),
			new luaL_Reg ("random", math_random),
			new luaL_Reg ("randomseed", math_randomseed),
			new luaL_Reg ("sin", math_sin),
			new luaL_Reg ("sqrt", math_sqrt),
			new luaL_Reg ("tan", math_tan),
			new luaL_Reg ("type", math_type),
            /* placeholders */
			new luaL_Reg ("pi", null),
			new luaL_Reg ("huge", null),
			new luaL_Reg ("maxinteger", null),
			new luaL_Reg ("mininteger", null),
			new luaL_Reg (null, null),
		};
	}
	
	public static partial class mod {

        /*
        ** Open math library
        */
		public static int luaopen_math (lua_State L) {
            cc.luaL_newlib (L, imp.math_funcs);
            cc.lua_pushnumber (L, imp.PI);
            cc.lua_setfield (L, -2, "pi");
            cc.lua_pushnumber (L, imp.HUGE_VAL);
            cc.lua_setfield (L, -2, "huge");
            cc.lua_pushinteger (L, cc.LUA_MAXINTEGER);
            cc.lua_setfield (L, -2, "maxinteger");
            cc.lua_pushinteger (L, cc.LUA_MININTEGER);
            cc.lua_setfield (L, -2, "mininteger");
			return 1;
		}
	}
}