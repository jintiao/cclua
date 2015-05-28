﻿using System;

namespace cclua53 {


    public static partial class imp {

        private static class lobject {
            public class Value {
                public object o;
            }
        }

        /* Bit mark for collectable types */
        public const byte BIT_ISCOLLECTABLE = 1 << 6;

        /* mark a tag as collectable */
        public static byte ctb (byte t) {
            return (byte)(t | BIT_ISCOLLECTABLE);
        }

        public static void setnilvalue (TValue obj) {
            obj.tt_ = cclua.LUA_TNIL;
        }

        public static void sethvalue (cclua.lua_State L, TValue obj, GCObject x) {
            obj.value_.o = x;
            obj.tt_ = ctb (cclua.LUA_TTABLE);
        }


        /*
        ** Tagged Values. This is the basic representation of values in Lua,
        ** an actual value plus a tag with its type.
        */
        public class TValue {
            public lobject.Value value_;
            public int tt_;

            public TValue () {
                tt_ = cclua.LUA_TNIL;
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
            public byte extra;  /* reserved words for short strings; "has hash" for longs */
            public uint hash;
            public ulong len;  /* number of characters in string */
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

        private class TKey {
            public class Nk {
                public TValue tv;
                public int next;

                public Nk () {
                    tv = new TValue ();
                    next = 0;
                }
            }

            public Nk nk;
            public TValue tvk;

            public TKey () {
                nk = new Nk ();
                tvk = new TValue ();
            }
        }

        private class Node {
            public TValue i_val = new TValue ();
            public TKey i_key = new TKey ();

            public Node () {
                i_val = new TValue ();
                i_key = new TKey ();
            }

            public void setnext (int n) {
                i_key.nk.next = n;
            }

            public TValue wgkey () {
                return i_key.nk.tv;
            }

            public TValue gval () {
                return i_val;
            }
        }

        public class Table : GCObject {
            public byte flags;
            public byte lsizenode;
            public uint sizearray;
            public TValue[] array;
            public Node[] node;
            public Node lastfree;
            public Table metatable;
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

        public static int luaO_ceillog2 (uint x) {
            int l = 0;
            x--;
            while (x >= 256) { l += 8; x >>= 8; }
            return l + log_2[x];
        }
    }

}
