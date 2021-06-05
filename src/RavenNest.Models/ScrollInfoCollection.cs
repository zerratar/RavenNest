using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace RavenNest.Models
{
    public class ScrollInfoCollection : IEnumerable<ScrollInfo>
    {
        public ScrollInfo[] Scrolls { get; set; }
        public int Count => Scrolls?.Length ?? 0;

        public ScrollInfoCollection()
        {
        }

        public ScrollInfoCollection(IEnumerable<ScrollInfo> scrolls)
        {
            this.Scrolls = scrolls.ToArray();
        }

        public IEnumerator<ScrollInfo> GetEnumerator()
        {
            if (Scrolls == null)
            {
                yield break;
            }

            for (var i = 0; i < Scrolls.Length; ++i)
            {
                yield return Scrolls[i];
            }
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            if (this.Scrolls == null)
            {
                return null;
            }

            return this.Scrolls.GetEnumerator();
        }
    }
}
