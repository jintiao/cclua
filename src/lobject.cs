using System;

using lua_State = cclua.lua530.lua_State;

namespace cclua {
	
    public static partial class imp {
        
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

        /* tag with no variants (bits 0-3) */
        public static int novariant (int x) { return (x & 0x0F); }

        /* type tag of a TValue (bits 0-3 for tags + variant bits 4-5) */
        public static int ttype (TValue o) { return (rttype (o) & 0x3F); }

        /* type tag of a TValue with no variants (bits 0-3) */
        public static int ttnov (TValue o) { return novariant (rttype (o)); }


        /* Macros to test type */
        public static bool checktag (TValue o, int t) { return (rttype (o) == t); }
        public static bool checktype (TValue o, int t) { return (ttnov (o) == t); }
        public static bool ttisnumber (TValue o) { return checktype (o, lua530.LUA_TNUMBER); }
        public static bool ttisfloat (TValue o) { return checktag (o, LUA_TNUMFLT); }
        public static bool ttisinteger (TValue o) { return checktag (o, LUA_TNUMINT); }
        public static bool ttisnil (TValue o) { return checktag (o, lua530.LUA_TNIL); }
        public static bool ttisboolean (TValue o) { return checktag (o, lua530.LUA_TBOOLEAN); }
        public static bool ttislightuserdata (TValue o) { return checktag (o, lua530.LUA_TLIGHTUSERDATA); }
        public static bool ttisstring (TValue o) { return checktype (o, lua530.LUA_TSTRING); }
        public static bool ttisshrstring (TValue o) { return checktag (o, ctb (LUA_TSHRSTR)); }
        public static bool ttislngstring (TValue o) { return checktag (o, ctb (LUA_TLNGSTR)); }
        public static bool ttistable (TValue o) { return checktag (o, ctb (lua530.LUA_TTABLE)); }
        public static bool ttisfunction (TValue o) { return checktype (o, lua530.LUA_TFUNCTION); }
		public static bool ttisclosure (TValue o) { return ((ttype (o) & 0x1f) == lua530.LUA_TFUNCTION); }
		public static bool ttisCclosure (TValue o) { return checktag (o, ctb(LUA_TCCL)); }
		public static bool ttisLclosure (TValue o) { return checktag (o, ctb(LUA_TLCL)); }
		public static bool ttislcf (TValue o) { return checktag (o, LUA_TLCF); }
        public static bool ttisfulluserdata (TValue o) { return checktag (o, ctb (lua530.LUA_TUSERDATA)); }
        public static bool ttisthread (TValue o) { return checktag (o, ctb (lua530.LUA_TTHREAD)); }
		public static bool ttisdeadkey (TValue o) { return checktag (o, LUA_TDEADKEY); }
        
        

        /* Macros to access values */
        public static long ivalue (TValue o) { return check_exp<long> (ttisinteger (o), o.value_.o); }
        public static double fltvalue (TValue o) { return check_exp<double> (ttisfloat (o), o.value_.o); }
        public static GCObject gcvalue (TValue o) { return check_exp<GCObject> (iscollectable (o), o.value_.o); }
        public static object pvalue (TValue o) { return check_exp<object> (ttislightuserdata (o), o.value_.o); }
		public static TString tsvalue (TValue o) { return check_exp<TString> (ttisstring (o), o.value_.o); }
		public static Udata uvalue (TValue o) { return check_exp<Udata> (ttisfulluserdata (o), o.value_.o); }
		public static Closure clvalue (TValue o) { return check_exp<Closure> (ttisclosure (o), o.value_.o); }
		public static LClosure clLvalue (TValue o) { return check_exp<LClosure> (ttisLclosure (o), o.value_.o); }
		public static CClosure clCvalue (TValue o) { return check_exp<CClosure> (ttisCclosure (o), o.value_.o); }
        public static lua530.lua_CFunction fvalue (TValue o) { return check_exp<lua530.lua_CFunction> (ttislcf (o), o.value_.o); }
        public static Table hvalue (TValue o) { return check_exp<Table> (ttistable (o), o.value_.o); }
		public static long bvalue (TValue o) { return check_exp<long> (ttisboolean (o), o.value_.o); }
		public static lua_State thvalue (TValue o) { return check_exp<lua_State> (ttisthread (o), o.value_.o); }
		/* a dead value may get the 'gc' field, but cannot access its contents */
		public static object deadvalue (TValue o) { return check_exp<object> (ttisdeadkey (o), o.value_.o); }

		public static bool l_isfalse (TValue o) { return (ttisnil (o) || (ttisboolean (o) && bvalue (o) == 0)); }


		public static bool iscollectable (TValue o) { return ((rttype(o) & BIT_ISCOLLECTABLE) != 0); }



		/* Macros for internal tests */
		public static bool righttt (TValue obj) { return (ttype (obj) == gcvalue (obj).tt); }

		public static void checkliveness (global_State g, TValue obj) {
			lua_longassert ( (iscollectable (obj) == false) ||
			                (righttt (obj) && (isdead (g, gcvalue (obj)) == false)));
		}


        /* Macros to set values */

		public static void settt_ (TValue o, int t) { o.tt_ = t; }

		public static void setfltvalue (TValue obj, double x) { obj.value_.o = x; settt_ (obj, LUA_TNUMFLT); }

		public static void setivalue (TValue obj, long x) { obj.value_.o = x; settt_ (obj, LUA_TNUMINT); }

		public static void setnilvalue (TValue obj) { obj.value_.o = null; settt_ (obj, lua530.LUA_TNIL); }

		public static void setfvalue (TValue obj, lua530.lua_CFunction x) { obj.value_.o = x; settt_ (obj, LUA_TLCF); }
		
		public static void setpvalue (TValue obj, object x) { obj.value_.o = x; settt_ (obj, lua530.LUA_TLIGHTUSERDATA); }
		
		public static void setbvalue (TValue obj, long x) { obj.value_.o = x; settt_ (obj, lua530.LUA_TBOOLEAN); }
		
		public static void setgcovalue (TValue obj, GCObject x) { obj.value_.o = x; settt_ (obj, ctb (x.tt)); }
		
		public static void setsvalue (lua_State L, TValue obj, TString x) { 
			obj.value_.o = x; settt_ (obj, ctb (x.tt)); 
			checkliveness (G (L), obj);
		}
		
		public static void setuvalue (lua_State L, TValue obj, Udata x) {
            obj.value_.o = x; settt_ (obj, ctb (lua530.LUA_TUSERDATA)); 
			checkliveness (G (L), obj);
		}
		
		public static void setthvalue (lua_State L, TValue obj, lua_State x) {
            obj.value_.o = x; settt_ (obj, ctb (lua530.LUA_TTHREAD));
			checkliveness (G (L), obj);
		}
		
		public static void setclLvalue (lua_State L, TValue obj, LClosure x) { 
			obj.value_.o = x; settt_ (obj, ctb(LUA_TLCL)); 
			checkliveness (G (L), obj);
		}
		
		public static void setclCvalue (lua_State L, TValue obj, CClosure x) { 
			obj.value_.o = x; settt_ (obj, ctb(LUA_TCCL)); 
			checkliveness (G (L), obj);
		}
		
		public static void sethvalue (lua_State L, TValue obj, Table x) { 
			obj.value_.o = x; settt_ (obj, ctb (lua530.LUA_TTABLE)); 
			checkliveness (G (L), obj);
		}
		
		public static void setdeadvalue (TValue obj) { obj.value_.o = null; settt_ (obj, LUA_TDEADKEY); }




		public static void setobj (lua_State L, TValue obj1, TValue obj2) {
			obj1.value_.o = obj2.value_.o;
			obj1.tt_ = obj2.tt_;
			checkliveness (G (L), obj1);
		}

		/*
		** different types of assignments, according to destination
		*/
		
		/* from stack to (same) stack */
		public static void setobjs2s (lua_State L, TValue obj1, TValue obj2) { setobj (L, obj1, obj2); }
        /* to stack (not from same stack) */
		public static void setobj2s (lua_State L, TValue obj1, TValue obj2) { setobj (L, obj1, obj2); }
        public static void setsvalue2s (lua_State L, TValue obj1, TString obj2) { setsvalue (L, obj1, obj2); }
        public static void sethvalue2s (lua_State L, TValue obj1, Table obj2) { sethvalue (L, obj1, obj2); }
        //public static void setptvalue2s (lua_State L, TValue obj1, TValue obj2) { setptvalue (L, obj1, obj2); }
        /* from table to same table */
        public static void setobjt2t (lua_State L, TValue obj1, TValue obj2) { setobj (L, obj1, obj2); }
        /* to table */
        public static void setobj2t (lua_State L, TValue obj1, TValue obj2) { setobj (L, obj1, obj2); }
		/* to new object */
		public static void setobj2n (lua_State L, TValue obj1, TValue obj2) { setobj (L, obj1, obj2); }
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


        /*
        ** Header for string value; string bytes follow the end of this structure
        ** (aligned according to 'UTString'; see next).
        */
		public class TString : GCObject {
			public byte extra;  /* reserved words for short strings; "has hash" for longs */
            public long hash;
            public long len;  /* number of characters in string */
            public TString hnext;  /* linked list for hash table */
            public byte[] data;
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

		/* get the actual string (array of bytes) from a Lua value */
		public static byte[] svalue (TValue o) { return getstr (tsvalue (o)); }



		/*
		** Header for userdata; memory area follows the end of this structure
		** (aligned according to 'UUdata'; see next).
		*/
		public class Udata : GCObject {
			public byte ttuv_;  /* user value's tag */
			public Table metatable;
			public long len;  /* number of bytes */
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

		public static void getuservalue (lua_State L, Udata u, TValue o) {
			o.value_.o = u.user_.o;
			o.tt_ = u.ttuv_;
			checkliveness (G (L), o);
		}


		/*
		** Description of an upvalue for function prototypes
		*/
		public class Upvaldesc {
			TString name;  /* upvalue name (for debug information) */
			byte instack;  /* whether it is in stack */
			byte idx;  /* index of upvalue (in stack or in outer function's list) */
		}


		/*
		** Description of a local variable for function prototypes
		** (used for debug information)
		*/
		public class LocVar {
			TString varname;
			int startpc;  /* first point where variable is active */
			int endpc;  /* first point where variable is dead */
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
			public TValue k;  /* constants used by the function */
			public int code;
			public Proto[] p;  /* functions defined inside the function */
			public int lineinfo;  /* map from opcodes to source lines (debug information) */
			public LocVar locvars;  /* information about local variables (debug information) */
			public Upvaldesc upvalues;  /* upvalue information */
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
            public UpVal[] upvalue;  /* list of upvalues */
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


        private static byte[] log_2 = {
            0,1,2,2,3,3,3,3,4,4,4,4,4,4,4,4,5,5,5,5,5,5,5,5,5,5,5,5,5,5,5,5,
            6,6,6,6,6,6,6,6,6,6,6,6,6,6,6,6,6,6,6,6,6,6,6,6,6,6,6,6,6,6,6,6,
            7,7,7,7,7,7,7,7,7,7,7,7,7,7,7,7,7,7,7,7,7,7,7,7,7,7,7,7,7,7,7,7,
            7,7,7,7,7,7,7,7,7,7,7,7,7,7,7,7,7,7,7,7,7,7,7,7,7,7,7,7,7,7,7,7,
            8,8,8,8,8,8,8,8,8,8,8,8,8,8,8,8,8,8,8,8,8,8,8,8,8,8,8,8,8,8,8,8,
            8,8,8,8,8,8,8,8,8,8,8,8,8,8,8,8,8,8,8,8,8,8,8,8,8,8,8,8,8,8,8,8,
            8,8,8,8,8,8,8,8,8,8,8,8,8,8,8,8,8,8,8,8,8,8,8,8,8,8,8,8,8,8,8,8,
            8,8,8,8,8,8,8,8,8,8,8,8,8,8,8,8,8,8,8,8,8,8,8,8,8,8,8,8,8,8,8,8
        };

        public static int luaO_ceillog2 (long x) {
            int l = 0;
            x--;
            while (x >= 256) { l += 8; x >>= 8; }
            return l + log_2[x];
        }
    }

}
