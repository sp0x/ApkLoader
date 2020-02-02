using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace SharpAdbClient
{
    public class AsyncReceiver : MultiLineReceiver
    {
        private bool mDone;
        private Func<IEnumerable<string>, bool> mLineHandler;

        public AsyncReceiver(Func<IEnumerable<string>, bool> lineHandler)
        {
            ParsesErrors = true;
            mLineHandler = lineHandler;
        }
        protected override void ProcessNewLines(IEnumerable<string> lines)
        {
            if (mDone)
            {
                return; //We won't handle anything since we're done
            }
            if (mLineHandler != null)
            {
                mDone = mLineHandler(lines);
            }
        }

        public async Task WaitAsync()
        {
            while (!mDone)
            {
                await Task.Delay(1);
            }
        }
    }
}