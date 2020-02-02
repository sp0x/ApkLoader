using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Text;
using Microsoft.Extensions.FileProviders;

namespace CruzrUploader
{
    public class KeyHelper
    {
        public void CopyAdbKeys()
        {
            var embeddedProvider = new EmbeddedFileProvider(Assembly.GetExecutingAssembly());
            Stream adbKey = embeddedProvider.GetFileInfo("keys\\adbkey").CreateReadStream();
            Stream adbPubKey = embeddedProvider.GetFileInfo("keys\\adbkey.pub").CreateReadStream();
            Debug.Assert(adbKey != null, nameof(adbKey) + " != null");
            Debug.Assert(adbPubKey != null, nameof(adbPubKey) + " != null");
            var oDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".android");
            if (!Directory.Exists(oDir))
            {
                Directory.CreateDirectory(oDir);
            }

            var adbFile = Path.Combine(oDir, "adbkey");
            using (var ostream = File.OpenWrite(adbFile))
            {
                adbKey.CopyTo(ostream);
                adbKey.Flush();
                ostream.Flush();
            }

            var adbPubFile = Path.Combine(oDir, "adbkey.pub");
            using (var ostream = File.OpenWrite(adbPubFile))
            {
                adbPubKey.CopyTo(ostream);
                adbPubKey.Flush();
                ostream.Flush();
            }
        }
    }
}
