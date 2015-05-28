using System;

namespace cclua53
{
    public static partial class imp {

        /*
        ** Upvalues for Lua closures
        */
        public class UpVal {
            public class openc {
                UpVal next;  /* linked list */
                int touched;  /* mark to avoid cycles with dead threads */
            }
            public class uc {
                openc open;  /* (when open) */
                TValue value;  /* the value (when closed) */
            }

            public TValue v;  /* points to stack or to its own value */
            public ulong refcount;  /* reference counter */
            public uc u;
        }
    }
}
