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

            public static Node dummynode = new Node ();

            private static uint twoto (int x) {
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
                        n.setnext (0);
                        n.wgkey ().setnilvalue ();
                        n.gval ().setnilvalue ();
                    }
                }
                lsizenode = (byte)lsize;
                lastfree = gnode (size - 1);
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
        }

        public static void luaH_setint (lua_State L, Table t, long key, TValue value) {
        }
    }
}
