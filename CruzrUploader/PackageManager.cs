using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace CruzrUploader
{
    public class PackageManager
    {
        private string mSource;
        private string mDir;

        public PackageManager(String source, String outputDirectory)
        {
            mSource = source;
            mDir = outputDirectory;
        }

        public void AddPackage(string pkgname, bool force=false)
        {
            Uri uri = new Uri(new Uri(mSource), "/api/" + pkgname);
            var fileName = GetPath(pkgname);
            if (File.Exists(fileName) && !force)
            {
                return;
            }
            using (var wc = new WebClient())
            {
                wc.DownloadFile(uri, fileName);
            }
        }

        public string GetPath(string pkgname)
        {
            return Path.Combine(mDir, pkgname);
        }

        public Stream GetStream(string pkgname)
        {
            return File.OpenRead(GetPath(pkgname));
        }
    }
}
