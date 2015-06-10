using System;
using System.Text;

using lua_State = cclua.lua530.lua_State;

namespace cclua {
	
    public static partial class imp {

        public static class lobject {

            public static byte[] log_2 = {
                0,1,2,2,3,3,3,3,4,4,4,4,4,4,4,4,5,5,5,5,5,5,5,5,5,5,5,5,5,5,5,5,
                6,6,6,6,6,6,6,6,6,6,6,6,6,6,6,6,6,6,6,6,6,6,6,6,6,6,6,6,6,6,6,6,
                7,7,7,7,7,7,7,7,7,7,7,7,7,7,7,7,7,7,7,7,7,7,7,7,7,7,7,7,7,7,7,7,
                7,7,7,7,7,7,7,7,7,7,7,7,7,7,7,7,7,7,7,7,7,7,7,7,7,7,7,7,7,7,7,7,
                8,8,8,8,8,8,8,8,8,8,8,8,8,8,8,8,8,8,8,8,8,8,8,8,8,8,8,8,8,8,8,8,
                8,8,8,8,8,8,8,8,8,8,8,8,8,8,8,8,8,8,8,8,8,8,8,8,8,8,8,8,8,8,8,8,
                8,8,8,8,8,8,8,8,8,8,8,8,8,8,8,8,8,8,8,8,8,8,8,8,8,8,8,8,8,8,8,8,
                8,8,8,8,8,8,8,8,8,8,8,8,8,8,8,8,8,8,8,8,8,8,8,8,8,8,8,8,8,8,8,8
            };


            public static int l_str2d (byte[] s, ref double result) {
                bool ok = double.TryParse (byte2str (s), out result);
                if (ok == false) return 0;
                return s.Length;
            }


            public static int l_str2int (byte[] s, ref long result) {
                int p = 0;
                while (lisspace (s[p])) p++;  /* skip initial spaces */
                bool neg = isneg (s, ref p);
                bool empty = true;
                ulong a = 0;
                if (s[p] == '0' &&
                        (s[p + 1] == 'x' || s[p + 1] == 'X')) {  /* hex? */
                    p += 2;  /* skip '0x' */
                    for (; lisdigit (s[p]); p++) {
                        a = a * 16 + (ulong)luaO_hexavalue (s[p]);
                        empty = false;
                    }
                }
                else {  /* decimal */
                    for (; lisdigit (s[p]); p++) {
                        a = a * 10 + (ulong)(s[p] - '0');
                        empty = false;
                    }
                }
                while (lisspace (s[p])) p++;  /* skip trailing spaces */
                if (empty || p != s.Length + 1) return -1;  /* something wrong in the numeral */
                else {
                    result = (neg ? (long)(0u - a) : (long)a);
                    return p;
                }
            }


            public static bool isneg (byte[] s, ref int p) {
                if (s[p] == '-') { p++; return true; }
                else if (s[p] == '+') p++;
                return false;
            }


			/* maximum length of the conversion of a number to a string */
			public const int MAXNUMBER2STR = 50;
        }
        
        /*
        ** Extra tags for non-values
        */
        public const int LUA_TPROTO = lua530.LUA_NUMTAGS;
        public const int LUA_TDEADKEY = lua530.LUA_NUMTAGS + 1;
        
        /*
        ** number of all possible tags (including LUA_TNONE but excluding DEADKEY)
        */
        public const int LUA_TOTALTAGS = LUA_TPROTO + 2;
        

		/*
		** tags for Tagged Values have the following use of bits:
		** bits 0-3: actual tag (a LUA_T* value)
		** bits 4-5: variant bits
		** bit 6: whether value is collectable
		*/

		public const int VARBITS = 3 << 4;


        /*
        ** LUA_TFUNCTION variants:
        ** 0 - Lua function
        ** 1 - light C function
        ** 2 - regular C function (closure)
        */

        /* Variant tags for functions */
        public const int LUA_TLCL = (lua530.LUA_TFUNCTION | (0 << 4));  /* Lua closure */
        public const int LUA_TLCF = (lua530.LUA_TFUNCTION | (1 << 4));  /* light C function */
        public const int LUA_TCCL = (lua530.LUA_TFUNCTION | (2 << 4));  /* C closure */

		/* Variant tags for strings */
		public const int LUA_TSHRSTR = (lua530.LUA_TSTRING | (0 << 4));  /* short strings */
		public const int LUA_TLNGSTR = (lua530.LUA_TSTRING | (1 << 4));  /* long strings */

		/* Variant tags for numbers */
		public const int LUA_TNUMFLT = (lua530.LUA_TNUMBER | (0 << 4));  /* float numbers */
		public const int LUA_TNUMINT = (lua530.LUA_TNUMBER | (1 << 4));  /* integer numbers */


        /* Bit mark for collectable types */
        public const byte BIT_ISCOLLECTABLE = 1 << 6;

        /* mark a tag as collectable */
        public static byte ctb (byte t) { return (byte)(t | BIT_ISCOLLECTABLE); }


        /*
        ** Common type for all collectable objects
        */
        public class GCObject {
			public GCObject next;
            public byte tt;
			public byte marked;
        }
        
        
        
		public static Value val_ (TValue o) { return o.value_; }
        
        /* raw type tag of a TValue */
        public static int rttype (TValue o) { return o.tt_; }
        public static int rttype (lua_State L, int o) { return rttype (L.stack[o]); }

        /* tag with no variants (bits 0-3) */
        public static int novariant (int x) { return (x & 0x0F); }

        /* type tag of a TValue (bits 0-3 for tags + variant bits 4-5) */
        public static int ttype (TValue o) { return (rttype (o) & 0x3F); }
        public static int ttype (lua_State L, int o) { return ttype (L.stack[o]); }

        /* type tag of a TValue with no variants (bits 0-3) */
        public static int ttnov (TValue o) { return novariant (rttype (o)); }
        public static int ttnov (lua_State L, int o) { return ttnov (L.stack[o]); }


        /* Macros to test type */
        public static bool checktag (TValue o, int t) { return (rttype (o) == t); }
        public static bool checktag (lua_State L, int o, int t) { return checktag (L.stack[o], t); }
        public static bool checktype (TValue o, int t) { return (ttnov (o) == t); }
        public static bool checktype (lua_State L, int o, int t) { return checktype (L.stack[o], t); }
        public static bool ttisnumber (TValue o) { return checktype (o, lua530.LUA_TNUMBER); }
        public static bool ttisnumber (lua_State L, int o) { return ttisnumber (L.stack[o]); }
        public static bool ttisfloat (TValue o) { return checktag (o, LUA_TNUMFLT); }
        public static bool ttisfloat (lua_State L, int o) { return ttisfloat (L.stack[o]); }
        public static bool ttisinteger (TValue o) { return checktag (o, LUA_TNUMINT); }
        public static bool ttisinteger (lua_State L, int o) { return ttisinteger (L.stack[o]); }
        public static bool ttisnil (TValue o) { return checktag (o, lua530.LUA_TNIL); }
        public static bool ttisnil (lua_State L, int o) { return ttisnil (L.stack[o]); }
        public static bool ttisboolean (TValue o) { return checktag (o, lua530.LUA_TBOOLEAN); }
        public static bool ttisboolean (lua_State L, int o) { return ttisboolean (L.stack[o]); }
        public static bool ttislightuserdata (TValue o) { return checktag (o, lua530.LUA_TLIGHTUSERDATA); }
        public static bool ttislightuserdata (lua_State L, int o) { return ttislightuserdata (L.stack[o]); }
        public static bool ttisstring (TValue o) { return checktype (o, lua530.LUA_TSTRING); }
        public static bool ttisstring (lua_State L, int o) { return ttisstring (L.stack[o]); }
        public static bool ttisshrstring (TValue o) { return checktag (o, ctb (LUA_TSHRSTR)); }
        public static bool ttisshrstring (lua_State L, int o) { return ttisshrstring (L.stack[o]); }
        public static bool ttislngstring (TValue o) { return checktag (o, ctb (LUA_TLNGSTR)); }
        public static bool ttislngstring (lua_State L, int o) { return ttislngstring (L.stack[o]); }
        public static bool ttistable (TValue o) { return checktag (o, ctb (lua530.LUA_TTABLE)); }
        public static bool ttistable (lua_State L, int o) { return ttistable (L.stack[o]); }
        public static bool ttisfunction (TValue o) { return checktype (o, lua530.LUA_TFUNCTION); }
        public static bool ttisfunction (lua_State L, int o) { return ttisfunction (L.stack[o]); }
        public static bool ttisclosure (TValue o) { return ((ttype (o) & 0x1f) == lua530.LUA_TFUNCTION); }
        public static bool ttisclosure (lua_State L, int o) { return ttisclosure (L.stack[o]); }
        public static bool ttisCclosure (TValue o) { return checktag (o, ctb (LUA_TCCL)); }
        public static bool ttisCclosure (lua_State L, int o) { return ttisCclosure (L.stack[o]); }
        public static bool ttisLclosure (TValue o) { return checktag (o, ctb (LUA_TLCL)); }
        public static bool ttisLclosure (lua_State L, int o) { return ttisLclosure (L.stack[o]); }
        public static bool ttislcf (TValue o) { return checktag (o, LUA_TLCF); }
        public static bool ttislcf (lua_State L, int o) { return ttislcf (L.stack[o]); }
        public static bool ttisfulluserdata (TValue o) { return checktag (o, ctb (lua530.LUA_TUSERDATA)); }
        public static bool ttisfulluserdata (lua_State L, int o) { return ttisfulluserdata (L.stack[o]); }
        public static bool ttisthread (TValue o) { return checktag (o, ctb (lua530.LUA_TTHREAD)); }
        public static bool ttisthread (lua_State L, int o) { return ttisthread (L.stack[o]); }
        public static bool ttisdeadkey (TValue o) { return checktag (o, LUA_TDEADKEY); }
        public static bool ttisdeadkey (lua_State L, int o) { return ttisdeadkey (L.stack[o]); }
        
        

        /* Macros to access values */
        public static long ivalue (TValue o) { return check_exp<long> (ttisinteger (o), o.value_.o); }
        public static long ivalue (lua_State L, int o) { return ivalue (L.stack[o]); }
        public static double fltvalue (TValue o) { return check_exp<double> (ttisfloat (o), o.value_.o); }
        public static double fltvalue (lua_State L, int o) { return fltvalue (L.stack[o]); }
        public static double nvalue (TValue o) { return fltvalue (o); }
        public static double nvalue (lua_State L, int o) { return nvalue (L.stack[o]); }
        public static GCObject gcvalue (TValue o) { return check_exp<GCObject> (iscollectable (o), o.value_.o); }
        public static GCObject gcvalue (lua_State L, int o) { return gcvalue (L.stack[o]); }
        public static object pvalue (TValue o) { return check_exp<object> (ttislightuserdata (o), o.value_.o); }
        public static object pvalue (lua_State L, int o) { return pvalue (L.stack[o]); }
        public static TString tsvalue (TValue o) { return check_exp<TString> (ttisstring (o), o.value_.o); }
        public static TString tsvalue (lua_State L, int o) { return tsvalue (L.stack[o]); }
        public static Udata uvalue (TValue o) { return check_exp<Udata> (ttisfulluserdata (o), o.value_.o); }
        public static Udata uvalue (lua_State L, int o) { return uvalue (L.stack[o]); }
        public static Closure clvalue (TValue o) { return check_exp<Closure> (ttisclosure (o), o.value_.o); }
        public static Closure clvalue (lua_State L, int o) { return clvalue (L.stack[o]); }
        public static LClosure clLvalue (TValue o) { return check_exp<LClosure> (ttisLclosure (o), o.value_.o); }
        public static LClosure clLvalue (lua_State L, int o) { return clLvalue (L.stack[o]); }
        public static CClosure clCvalue (TValue o) { return check_exp<CClosure> (ttisCclosure (o), o.value_.o); }
        public static CClosure clCvalue (lua_State L, int o) { return clCvalue (L.stack[o]); }
        public static lua530.lua_CFunction fvalue (TValue o) { return check_exp<lua530.lua_CFunction> (ttislcf (o), o.value_.o); }
        public static lua530.lua_CFunction fvalue (lua_State L, int o) { return fvalue (L.stack[o]); }
        public static Table hvalue (TValue o) { return check_exp<Table> (ttistable (o), o.value_.o); }
        public static Table hvalue (lua_State L, int o) { return hvalue (L.stack[o]); }
        public static long bvalue (TValue o) { return check_exp<long> (ttisboolean (o), o.value_.o); }
        public static long bvalue (lua_State L, int o) { return bvalue (L.stack[o]); }
        public static lua_State thvalue (TValue o) { return check_exp<lua_State> (ttisthread (o), o.value_.o); }
        public static lua_State thvalue (lua_State L, int o) { return thvalue (L.stack[o]); }
		/* a dead value may get the 'gc' field, but cannot access its contents */
        public static object deadvalue (TValue o) { return check_exp<object> (ttisdeadkey (o), o.value_.o); }
        public static object deadvalue (lua_State L, int o) { return deadvalue (L.stack[o]); }

        public static bool l_isfalse (TValue o) { return (ttisnil (o) || (ttisboolean (o) && bvalue (o) == 0)); }
        public static bool l_isfalse (lua_State L, int o) { return l_isfalse (L.stack[o]); }


        public static bool iscollectable (TValue o) { return ((rttype (o) & BIT_ISCOLLECTABLE) != 0); }
        public static bool iscollectable (lua_State L, int o) { return iscollectable (L.stack[o]); }



		/* Macros for internal tests */
        public static bool righttt (TValue obj) { return (ttype (obj) == gcvalue (obj).tt); }
        public static bool righttt (lua_State L, int o) { return righttt (L.stack[o]); }

		public static void checkliveness (global_State g, TValue obj) {
			lua_longassert ( (iscollectable (obj) == false) ||
			                (righttt (obj) && (isdead (g, gcvalue (obj)) == false)));
		}
        public static void checkliveness (global_State g, lua_State L, int o) { checkliveness (g, L.stack[o]); }


        /* Macros to set values */

        public static void settt_ (TValue o, int t) { o.tt_ = t; }
        public static void settt_ (lua_State L, int o, int t) { settt_ (L.stack[o], t); }

        public static void setfltvalue (TValue obj, double x) { obj.value_.o = x; settt_ (obj, LUA_TNUMFLT); }
        public static void setfltvalue (lua_State L, int o, double x) { setfltvalue (L.stack[o], x); }

        public static void setivalue (TValue obj, long x) { obj.value_.o = x; settt_ (obj, LUA_TNUMINT); }
        public static void setivalue (lua_State L, int o, long t) { setivalue (L.stack[o], t); }

        public static void setnilvalue (TValue obj) { obj.value_.o = null; settt_ (obj, lua530.LUA_TNIL); }
        public static void setnilvalue (lua_State L, int o) { setnilvalue (L.stack[o]); }

        public static void setfvalue (TValue obj, lua530.lua_CFunction x) { obj.value_.o = x; settt_ (obj, LUA_TLCF); }
        public static void setfvalue (lua_State L, int o, lua530.lua_CFunction t) { setfvalue (L.stack[o], t); }

        public static void setpvalue (TValue obj, object x) { obj.value_.o = x; settt_ (obj, lua530.LUA_TLIGHTUSERDATA); }
        public static void setpvalue (lua_State L, int o, object t) { setpvalue (L.stack[o], t); }

        public static void setbvalue (TValue obj, int x) { obj.value_.o = x; settt_ (obj, lua530.LUA_TBOOLEAN); }
        public static void setbvalue (lua_State L, int o, int t) { setbvalue (L.stack[o], t); }

        public static void setgcovalue (lua_State L, TValue obj, GCObject x) { obj.value_.o = x; settt_ (obj, ctb (x.tt)); }
        public static void setgcovalue (lua_State L, int o, GCObject t) { setgcovalue (L, L.stack[o], t); }
		
		public static void setsvalue (lua_State L, TValue obj, TString x) { 
			obj.value_.o = x; settt_ (obj, ctb (x.tt)); 
			checkliveness (G (L), obj);
		}
        public static void setsvalue (lua_State L, int o, TString t) { setsvalue (L, L.stack[o], t); }
		
		public static void setuvalue (lua_State L, TValue obj, Udata x) {
            obj.value_.o = x; settt_ (obj, ctb (lua530.LUA_TUSERDATA)); 
			checkliveness (G (L), obj);
		}
        public static void setuvalue (lua_State L, int o, Udata t) { setuvalue (L, L.stack[o], t); }
		
		public static void setthvalue (lua_State L, TValue obj, lua_State x) {
            obj.value_.o = x; settt_ (obj, ctb (lua530.LUA_TTHREAD));
			checkliveness (G (L), obj);
		}
        public static void setthvalue (lua_State L, int o, lua_State t) { setthvalue (L, L.stack[o], t); }
		
		public static void setclLvalue (lua_State L, TValue obj, LClosure x) { 
			obj.value_.o = x; settt_ (obj, ctb(LUA_TLCL)); 
			checkliveness (G (L), obj);
		}
        public static void setclLvalue (lua_State L, int o, LClosure t) { setclLvalue (L, L.stack[o], t); }
		
		public static void setclCvalue (lua_State L, TValue obj, CClosure x) { 
			obj.value_.o = x; settt_ (obj, ctb(LUA_TCCL)); 
			checkliveness (G (L), obj);
		}
        public static void setclCvalue (lua_State L, int o, CClosure t) { setclCvalue (L, L.stack[o], t); }
		
		public static void sethvalue (lua_State L, TValue obj, Table x) { 
			obj.value_.o = x; settt_ (obj, ctb (lua530.LUA_TTABLE)); 
			checkliveness (G (L), obj);
		}
        public static void sethvalue (lua_State L, int o, Table t) { sethvalue (L, L.stack[o], t); }

        public static void setdeadvalue (TValue obj) { obj.value_.o = null; settt_ (obj, LUA_TDEADKEY); }
        public static void setdeadvalue (lua_State L, int o) { setdeadvalue (L.stack[o]); }




		public static void setobj (lua_State L, TValue obj1, TValue obj2) {
			obj1.value_.o = obj2.value_.o;
			obj1.tt_ = obj2.tt_;
			checkliveness (G (L), obj1);
		}
        public static void setobj (lua_State L, int obj1, TValue obj2) { setobj (L, L.stack[obj1], obj2); }
        public static void setobj (lua_State L, TValue obj1, int obj2) { setobj (L, obj1, L.stack[obj2]); }
        public static void setobj (lua_State L, int obj1, int obj2) { setobj (L, L.stack[obj1], L.stack[obj2]); }

		/*
		** different types of assignments, according to destination
		*/
		
		/* from stack to (same) stack */
        public static void setobjs2s (lua_State L, TValue obj1, TValue obj2) { setobj (L, obj1, obj2); }
        public static void setobjs2s (lua_State L, int obj1, int obj2) { setobj (L, obj1, obj2); }
        /* to stack (not from same stack) */
        public static void setobj2s (lua_State L, TValue obj1, TValue obj2) { setobj (L, obj1, obj2); }
        public static void setobj2s (lua_State L, int obj1, TValue obj2) { setobj (L, obj1, obj2); }
        public static void setobj2s (lua_State L, int obj1, int obj2) { setobj (L, obj1, obj2); }
        public static void setsvalue2s (lua_State L, TValue obj1, TString obj2) { setsvalue (L, obj1, obj2); }
        public static void setsvalue2s (lua_State L, int obj1, TString obj2) { setsvalue (L, obj1, obj2); }
        public static void sethvalue2s (lua_State L, TValue obj1, Table obj2) { sethvalue (L, obj1, obj2); }
        public static void sethvalue2s (lua_State L, int obj1, Table obj2) { sethvalue (L, obj1, obj2); }
        /* from table to same table */
        public static void setobjt2t (lua_State L, TValue obj1, TValue obj2) { setobj (L, obj1, obj2); }
        public static void setobjt2t (lua_State L, int obj1, int obj2) { setobj (L, obj1, obj2); }
        /* to table */
        public static void setobj2t (lua_State L, TValue obj1, TValue obj2) { setobj (L, obj1, obj2); }
        public static void setobj2t (lua_State L, TValue obj1, int obj2) { setobj (L, obj1, obj2); }
        public static void setobj2t (lua_State L, int obj1, int obj2) { setobj (L, obj1, obj2); }
		/* to new object */
        public static void setobj2n (lua_State L, TValue obj1, TValue obj2) { setobj (L, obj1, obj2); }
        public static void setobj2n (lua_State L, TValue obj1, int obj2) { setobj (L, obj1, obj2); }
        public static void setobj2n (lua_State L, int obj1, int obj2) { setobj (L, obj1, obj2); }
        public static void setsvalue2n (lua_State L, TValue obj1, TString obj2) { setsvalue (L, obj1, obj2); }




		/*
		** {======================================================
		** types and prototypes
		** =======================================================
		*/


		/*
		** Union of all Lua values
		*/
	    public class Value {
            public object o;
        }
		
		/*
        ** Tagged Values. This is the basic representation of values in Lua,
        ** an actual value plus a tag with its type.
        */
        public class TValue {
            public Value value_;
            public int tt_;

			public TValue () {
                value_ = luaM_newobject<Value> (null);
                tt_ = lua530.LUA_TNIL;
            }
        }

        public static void tvcopy (TValue o1, TValue o2) {
            o1.value_ = o2.value_;
            o1.tt_ = o2.tt_;
        }


        /*
        ** Header for string value; string bytes follow the end of this structure
        ** (aligned according to 'UTString'; see next).
        */
		public class TString : GCObject {
			public byte extra;  /* reserved words for short strings; "has hash" for longs */
            public long hash;
            public int len;  /* number of characters in string */
            public TString hnext;  /* linked list for hash table */
            public byte[] data;
            public int offset;
        }

        /*
        ** Ensures that address after this type is always fully aligned.
        */
		public class UTString {
			public double dummy;  /* ensures maximum alignment for strings */
			public TString tsv;
        }


		/*
		** Get the actual string (array of bytes) from a 'TString'.
		** (Access to 'extra' ensures that value is really a 'TString'.)
		*/
		public static byte[] getstr (TString str) { return str.data; }
		public static string getsstr (TString str) { return byte2str (str.data); }

		/* get the actual string (array of bytes) from a Lua value */
		public static byte[] svalue (TValue o) { return getstr (tsvalue (o)); }
        public static byte[] svalue (lua_State L, int o) { return svalue (L.stack[o]); }



		/*
		** Header for userdata; memory area follows the end of this structure
		** (aligned according to 'UUdata'; see next).
		*/
		public class Udata : GCObject {
			public byte ttuv_;  /* user value's tag */
			public Table metatable;
			public int len;  /* number of bytes */
			public Value user_;  /* user value */
			public byte[] data;

			public Udata () {
				user_ = luaM_newobject<Value> (null);
			}
		}


		/*
		** Ensures that address after this type is always fully aligned.
		*/
		public class UUdata {
			public double dummy;  /* ensures maximum alignment for strings */
			public Udata uv;
		}


		/*
		**  Get the address of memory block inside 'Udata'.
		** (Access to 'ttuv_' ensures that value is really a 'Udata'.)
		*/
		public static byte[] getudatamem (Udata u) { return u.data; }

		public static void setuservalue (lua_State L, Udata u, TValue o) {
			u.user_.o = o.value_.o;
			u.ttuv_ = (byte)o.tt_;
			checkliveness (G (L), o);
		}
        public static void setuservalue (lua_State L, Udata u, int o) { setuservalue (L, u, L.stack[o]); }


		public static void getuservalue (lua_State L, Udata u, TValue o) {
			o.value_.o = u.user_.o;
			o.tt_ = u.ttuv_;
			checkliveness (G (L), o);
		}
        public static void getuservalue (lua_State L, Udata u, int o) { getuservalue (L, u, L.stack[o]); }


		/*
		** Description of an upvalue for function prototypes
		*/
		public class Upvaldesc {
            public TString name;  /* upvalue name (for debug information) */
            public byte instack;  /* whether it is in stack */
            public byte idx;  /* index of upvalue (in stack or in outer function's list) */
		}


		/*
		** Description of a local variable for function prototypes
		** (used for debug information)
		*/
		public class LocVar {
            public TString varname;
            public int startpc;  /* first point where variable is active */
            public int endpc;  /* first point where variable is dead */
		}


		/*
		** Function Prototypes
		*/
		public class Proto : GCObject {
			public byte numparams;  /* number of fixed parameters */
			public byte is_vararg;
			public byte maxstacksize;  /* maximum stack used by this function */
			public int sizeupvalues;  /* size of 'upvalues' */
			public int sizek;  /* size of 'k' */
			public int sizecode;
			public int sizelineinfo;
			public int sizep;  /* size of 'p' */
			public int sizelocvars;
			public int linedefined;
			public int lastlinedefined;
			public TValue[] k;  /* constants used by the function */
			public uint[] code;
			public Proto[] p;  /* functions defined inside the function */
			public int[] lineinfo;  /* map from opcodes to source lines (debug information) */
			public LocVar[] locvars;  /* information about local variables (debug information) */
			public Upvaldesc[] upvalues;  /* upvalue information */
			public LClosure cache;  /* last created closure with this prototype */
			public TString source;  /* used for debug information */
			public GCObject gclist;
		}



		/*
		** Closures
		*/
		public class ClosureHeader : GCObject {
			public byte nupvalues;
			public GCObject gclist;
		}

		public class CClosure : ClosureHeader {
            public lua530.lua_CFunction f;
            public TValue[] upvalue;  /* list of upvalues */
		}

		public class LClosure : ClosureHeader {
            public Proto p;
            public UpVal[] upvals;  /* list of upvalues */
		}

		public class Closure {
			public CClosure c;
			public LClosure l;

			public Closure () {
				c = luaM_newobject<CClosure> (null);
                l = luaM_newobject<LClosure> (null);
			}
		}


		public static bool isLfunction (TValue o) { return ttisLclosure (o); }

		public static Proto getproto (TValue o) { return clLvalue (o).p; }



		/*
		** Tables
		*/
        public class TKey {
            public TValue tvk;
            public Node next;

            public TKey () {
				tvk = luaM_newobject<TValue> (null);
                next = null;
            }
		}


		/* copy a value into a key without messing up field 'next' */
		public static void setnodekey (lua_State L, TKey key, TValue obj) {
			key.tvk.value_ = obj.value_;
			key.tvk.tt_ = obj.tt_;
			checkliveness (G (L), obj);
		}


        public class Node {
			public TValue i_val;
			public TKey i_key;
            public long index;

            public Node () {
				i_val = luaM_newobject<TValue> (null);
				i_key = luaM_newobject<TKey> (null);
                index = 0;
            }
        }

        public class Table : GCObject {
            public byte flags;
            public byte lsizenode;
            public long sizearray;
            public TValue[] array;
            public Node[] node;
            public long lastfree;
            public Table metatable;
			public GCObject gclist;
        }



        /*
        ** 'module' operation for hashing (size is always a power of 2)
        */
        public static long lmod (long s, long size) { return (s & (size - 1)); }

        public static long twoto (int x) { return (1 << x); }
        public static long sizenode (Table t) { return twoto (t.lsizenode); }


		/*
		** (address of) a fixed nil value
		*/
		public static TValue luaO_nilobject = new TValue ();


        /* size of buffer for 'luaO_utf8esc' function */
        public const int UTF8BUFFSZ = 8;


        /*
        ** converts an integer to a "floating point byte", represented as
        ** (eeeeexxx), where the real value is (1xxx) * 2^(eeeee - 1) if
        ** eeeee != 0 and (xxx) otherwise.
        */
        public static int luaO_int2fb (uint x) {
            int e = 0;
            if (x < 8) return (int)x;
            while (x >= 0x10) {
                x = (x + 1) >> 1;
                e++;
            }
            return (((e + 1) << 3) | ((int)(x - 8)));
        }


        /* converts back */
        public static int luaO_fb2int (int x) {
            int e = (x >> 3) & 0x1f;
            if (e == 0) return x;
            else return ((x & 7) + 8) << (e - 1);
        }


        public static int luaO_ceillog2 (long x) {
            int l = 0;
            x--;
            while (x >= 256) { l += 8; x >>= 8; }
            return l + lobject.log_2[x];
        }


        public static void luaO_arith (lua_State L, int op, TValue p1, TValue p2, TValue res) {
            if (op == lua530.LUA_OPBAND ||
                    op == lua530.LUA_OPBOR ||
                    op == lua530.LUA_OPBXOR ||
                    op == lua530.LUA_OPSHL ||
                    op == lua530.LUA_OPSHR ||
                    op == lua530.LUA_OPBNOT) {  /* operate only on integers */
                long i1 = 0;
                long i2 = 0;
                if (tointeger (p1, ref i1) && tointeger (p2, ref i2)) {
                }
                    
            }
        }
        public static void luaO_arith (lua_State L, int op, int p1, int p2, int res) { luaO_arith (L, op, L.stack[p1], L.stack[p2], L.stack[res]); }



        public static int luaO_hexavalue (int c) {
            if (lisdigit (c)) return (c - '0');
            else return (ltolower (c) - 'a' + 10);
        }


        public static int luaO_str2num (byte[] s, TValue o) {
            long i = 0;
            double n = 0;
            int e = lobject.l_str2int (s, ref i);  /* try as an integer */
            if (e > 0) {
                setivalue (o, i);
            }
            else {
                e = lobject.l_str2d (s, ref n);  /* else try as a float */
                if (e > 0) {
                    setfltvalue (o, n);
                }
                else
                    return 0;  /* conversion failed */
            }
            return e;  /* success; return string size */
        }


        public static int luaO_utf8esc (byte[] buff, ulong x) {
            int n = 1;  /* number of bytes put in buffer (backwards) */
            lua_assert (x <= 0x10FFFF);
            if (x < 0x80)  /* ascii? */
                buff[UTF8BUFFSZ - 1] = (byte)x;
            else {  /* need continuation bytes */
                uint mfb = 0x3f;  /* maximum that fits in first byte */
                do {  /* add continuation bytes */
                    buff[UTF8BUFFSZ - (n++)] = (byte)(0x80 | (x & 0x3f));
                    x >>= 6;  /* remove added bits */
                    mfb >>= 1;  /* now there is one less bit available in first byte */
                } while (x > mfb);  /* still needs continuation byte? */
                buff[UTF8BUFFSZ - n] = (byte)(((~mfb) << 1) | x);  /* add first byte */
            }
            return n;
        }


		/*
		** Convert a number object to a string
		*/
		public static void luaO_tostring (lua_State L, TValue obj) {
            lua_assert (ttisnumber (obj));
			string str;
            if (ttisinteger (obj))
                str = lua_integer2str (ivalue (obj));
			else {
                double n = fltvalue (obj);
                double f = l_floor (n);
                str = lua_number2str (n);
				if (n == f) {  /* looks like an int? */
                    str = str + ".0";  /* adds '.0' to result */
				}
            }
            byte[] buf = str2byte (str);
            setsvalue2s (L, obj, luaS_newlstr (L, buf, buf.Length));
		}
        public static void luaO_tostring (lua_State L, int obj) {
            luaO_tostring (L, L.stack[obj]);
        }


        public static string luaO_pushfstring (lua_State L, string fmt, params object[] args) {
            return fmt;
        }


        public static void luaO_chunkid (byte[] o, byte[] source, int bufflen) {
        }
























    }

}
