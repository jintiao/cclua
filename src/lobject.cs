using System;

namespace cclua53 {


    public static partial class imp {

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
        public const int LUA_TLCL = (cclua.LUA_TFUNCTION | (0 << 4));  /* Lua closure */
        public const int LUA_TLCF = (cclua.LUA_TFUNCTION | (1 << 4));  /* light C function */
        public const int LUA_TCCL = (cclua.LUA_TFUNCTION | (2 << 4));  /* C closure */

		/* Variant tags for strings */
		public const int LUA_TSHRSTR = (cclua.LUA_TSTRING | (0 << 4));  /* short strings */
		public const int LUA_TLNGSTR = (cclua.LUA_TSTRING | (1 << 4));  /* long strings */

		/* Variant tags for numbers */
		public const int LUA_TNUMFLT = (cclua.LUA_TNUMBER | (0 << 4));  /* float numbers */
		public const int LUA_TNUMINT = (cclua.LUA_TNUMBER | (1 << 4));  /* integer numbers */

        /* Bit mark for collectable types */
        public const byte BIT_ISCOLLECTABLE = 1 << 6;

        /* mark a tag as collectable */
        public static byte ctb (byte t) { return (byte)(t | BIT_ISCOLLECTABLE); }

        /* raw type tag of a TValue */
        public static int rttype (TValue o) { return o.tt_; }

        /* tag with no variants (bits 0-3) */
        public static int novariant (int x) { return (x & 0x0F); }

        /* type tag of a TValue (bits 0-3 for tags + variant bits 4-5) */
        public static int ttype (TValue o) { return (rttype (o) & 0x3F); }

        /* type tag of a TValue with no variants (bits 0-3) */
        public static int ttnov (TValue o) { return novariant (rttype (o)); }

        /* Macros to test type */
        public static bool checktag (TValue o, int t) { return (o.tt_ == t); }
        public static bool checktype (TValue o, int t) { return (ttnov (o) == t); }
        public static bool ttisnumber (TValue o) { return checktype (o, cclua.LUA_TNUMBER); }
        public static bool ttisfloat (TValue o) { return checktag (o, LUA_TNUMFLT); }
        public static bool ttisinteger (TValue o) { return checktag (o, LUA_TNUMINT); }
        public static bool ttisnil (TValue o) { return checktag (o, cclua.LUA_TNIL); }
        public static bool ttisboolean (TValue o) { return checktag (o, cclua.LUA_TBOOLEAN); }
        public static bool ttislightuserdata (TValue o) { return checktag (o, cclua.LUA_TLIGHTUSERDATA); }
        public static bool ttisstring (TValue o) { return checktype (o, cclua.LUA_TSTRING); }
        public static bool ttisshrstring (TValue o) { return checktag (o, ctb (LUA_TSHRSTR)); }
        public static bool ttislngstring (TValue o) { return checktag (o, ctb (LUA_TLNGSTR)); }
        public static bool ttistable (TValue o) { return checktag (o, ctb (cclua.LUA_TTABLE)); }
        public static bool ttisfunction (TValue o) { return checktype (o, cclua.LUA_TFUNCTION); }
        
        public static bool iscollectable (TValue o) { return ((rttype(o) & BIT_ISCOLLECTABLE) != 0); }

        /* Macros to access values */
        public static long ivalue (TValue o) { return check_exp<long> (ttisinteger (o), o.value_.o); }
        public static double fltvalue (TValue o) { return check_exp<double> (ttisfloat (o), o.value_.o); }
        public static GCObject gcvalue (TValue o) { return check_exp<GCObject> (iscollectable (o), o.value_.o); }
        public static object pvalue (TValue o) { return check_exp<object> (ttislightuserdata (o), o.value_.o); }
        public static TString tsvalue (TValue o) { return check_exp<TString> (ttisstring (o), o.value_.o); }
        public static Table hvalue (TValue o) { return check_exp<Table> (ttistable (o), o.value_.o); }
        public static long bvalue (TValue o) { return check_exp<long> (ttisboolean (o), o.value_.o); }


        /* Macros to set values */

        public static void setivalue (TValue obj, long x) {
            obj.value_.o = x;
            obj.tt_ = LUA_TNUMINT;
        }

        public static void setnilvalue (TValue obj) {
            obj.tt_ = cclua.LUA_TNIL;
        }

        public static void sethvalue (cclua.lua_State L, TValue obj, GCObject x) {
			obj.value_.o = obj2gco (x);
            obj.tt_ = ctb (cclua.LUA_TTABLE);
        }

		public static void setobj (cclua.lua_State L, TValue obj1, TValue obj2) {
			obj1.copy (obj2);
		}

		/*
		** different types of assignments, according to destination
		*/
		
		/* from stack to (same) stack */

		public static void setobjs2s (cclua.lua_State L, TValue obj1, TValue obj2) {
			setobj (L, obj1, obj2);
		}

        /* to stack (not from same stack) */

        public static void setobj2s (cclua.lua_State L, TValue obj1, TValue obj2) {
            setobj (L, obj1, obj2);
        }

        /* from table to same table */

        public static void setobjt2t (cclua.lua_State L, TValue obj1, TValue obj2) {
            setobj (L, obj1, obj2);
        }

        /* to table */

        public static void setobj2t (cclua.lua_State L, TValue obj1, TValue obj2) {
            setobj (L, obj1, obj2);
        }


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
				Value = new Value ();
                tt_ = cclua.LUA_TNIL;
            }

			public void copy (TValue obj) {
				value_.o = obj.value_.o;
				tt_ = obj.tt_;
			}
        }

        /*
        ** Common type for all collectable objects
        ** Common type has only the common header
        */
        public class GCObject {
            public byte tt;
        }

        /*
        ** Header for string value; string bytes follow the end of this structure
        ** (aligned according to 'UTString'; see next).
        */
        public class TString : GCObject {
            public long hash;
            public long len;  /* number of characters in string */
            public TString hnext;  /* linked list for hash table */
            public byte[] data;
            public byte extra;  /* reserved words for short strings; "has hash" for longs */
        }

        /*
        ** Ensures that address after this type is always fully aligned.
        */
        public class UTString {
            public double dummy;  /* ensures maximum alignment for strings */
            public TString tsv;
        }

        public class TKey {
            public TValue tvk;
            public Node next;

            public TKey () {
                tvk = new TValue ();
                next = null;
            }
        }

        public class Node {
            public TValue i_val = new TValue ();
            public TKey i_key = new TKey ();
            public long index;

            public Node () {
                i_val = new TValue ();
                i_key = new TKey ();
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
        }


        /* copy a value into a key without messing up field 'next' */
        public static void setnodekey (cclua.lua_State L, TKey key, TValue obj) {
            key.tvk.value_ = obj.value_;
            key.tvk.tt_ = obj.tt_;
        }

        /*
        ** 'module' operation for hashing (size is always a power of 2)
        */
        public static long lmod (long s, long size) {
            return (s & (size - 1));
        }

        public static long twoto (int x) {
            return (1 << x);
        }

        public static long sizenode (Table t) {
            return twoto (t.lsizenode);
        }

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
