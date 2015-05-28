using System;

namespace cclua53
{

    public static partial class imp {

        private static class ltable {

            /*
            ** Maximum size of array part (MAXASIZE) is 2^MAXABITS. MAXABITS is
            ** the largest integer such that MAXASIZE fits in an unsigned int.
            */
            public const int CHAR_BIT = 8;
            public const int MAXABITS = sizeof (int) * CHAR_BIT - 1;

            /*
            ** Maximum size of hash part is 2^MAXHBITS. MAXHBITS is the largest
            ** integer such that 2^MAXHBITS fits in a signed int. (Note that the
            ** maximum number of elements in a table, 2^MAXABITS + 2^MAXHBITS, still
            ** fits comfortably in an unsigned int.)
            */
            public const int MAXHBITS = MAXABITS - 1;

			public static Node[] dummynode = { new Node () }

			public static uint twoto (int x) {
                return (uint)(1 << x);
            }

            private Node gnode (int i) {
                return node[i];
            }

            public static void setnodevector (cclua.lua_State L, Table t, uint size) {
                int lsize;
                if (size == 0) {
                    t.node = dummynode;
                    lsize = 0;
                }
                else {
                    lsize = luaO_ceillog2 (size);
                    if (lsize > MAXHBITS)
                        luaG_runerror (L, "table overflow");
                    size = twoto (lsize);
                    t.node = luaM_newvector<Node> (L, size);
                    for (int i = 0; i < (int)size; i++) {
                        Node n = gnode (i);
						n.i_key.nk.next = 0;
						setnilvalue (n.i_key.nk.tv);
						setnilvalue (n.i_val);
                    }
                }
                lsizenode = (byte)lsize;
                lastfree = gnode (size - 1);
            }

			public static void setarrayvector (cclua.lua_State L, Table t, uint size) {
				luaM_reallocvector<TValue> (L, ref t.array, t.sizearray, size);
				for (uint i = t.sizearray; i < size; i++)
					setnilvalue (t.array[i]);
				t.sizearray = size;
			}
        }
        

        public static Table luaH_new (cclua.lua_State L) {
            Table t = luaC_newobj<Table> (L, cclua.LUA_TTABLE);
            t.metatable = null;
            t.flags = unchecked ((byte)(~0u));
            t.array = null;
            t.sizearray = 0;
            ltable.setnodevector (L, t, 0);
            return t;
        }

        public static void luaH_resize (lua_State L, Table t, uint nasize, uint nhsize) {
			uint oldasize = t.sizearray;
			int oldhsize = t.lsizenode;
			Node[] nold = t.node;  /* save old hash ... */
			if (nasize > oldasize)  /* array part must grow? */
				ltable.setarrayvector (L, t, nasize);
			/* create new hash part with appropriate size */
			ltable.setnodevector (L, t, nhsize);
			if (nasize < oldasize) {  /* array part must shrink? */
				t.sizearray = nasize;
				/* re-insert elements from vanishing slice */
				for (uint i = nasize; i < oldasize; i++) {
					if (ttisnil (t.array[i]) == false)
						luaH_setint (L, t, i + 1, t.array[i]);
				}
				/* shrink array */
				luaM_reallocvector<TValue> (L, ref t.array, oldasize, nasize);
			}
			/* re-insert elements from hash part */
			for (uint j = ltable.twoto (oldhsize) - 1; j >= 0; j--) {
				Node old = nold[j];
				if (ttisnil (old.i_val) == false) {
					/* doesn't need barrier/invalidate cache, as entry was
         				already present in the table */
					setobjt2t (L, luaH_set (L, t, old.i_key.tvk), old.i_val);
				}
			}
			if (isdummy (nold) == false)
				luaM_freevector (L, nold);  /* free old array */
        }

        public static void luaH_setint (lua_State L, Table t, long key, TValue value) {
        }
    }
}
