using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace LibAppDeployer
{
    public class PackageDownloadedEventArgs : EventArgs
    { 
        public AndroidPackage Package { get; set; }
    }

    public class PackageManager
    {
        public event EventHandler<PackageDownloadedEventArgs> PackageDownloaded;
        public event EventHandler<PackageDownloadedEventArgs> PackageLoaded;
        private Uri mSource;
        private string mDir;
        private List<AndroidPackage> mPackages;

        public PackageManager(Uri source, String outputDirectory)
        {
            mSource = source;
            mDir = outputDirectory;
            mPackages = new List<AndroidPackage>();
        }

        public List<AndroidPackage> GetPackages()
        {
            return mPackages;
        }

        public static PackageManager CreateWithUrl(Uri url)
        {
            string outputDir = Path.Combine(Directory.GetCurrentDirectory(), "packages");
            if (!Directory.Exists(outputDir))
            {
                Directory.CreateDirectory(outputDir);
            }
            var pm = new PackageManager(url, outputDir);
            return pm;
        }

        public static PackageManager CreateWithUrlAndFetch(Uri url, params string[] packageNames)
        {
            var pm = CreateWithUrl(url);
            foreach (var pkg in packageNames)
            {
                Console.WriteLine($"Downloading package: {pkg}");
                pm.AddPackage(pkg);
            }
            return pm;
        }

        public void AddPackage(string pkgname, bool force=false)
        {
            Uri uri = new Uri(mSource, "/api/" + pkgname);
            var fileName = GetPath(pkgname);
            if (File.Exists(fileName) && !force)
            {
                var npkg = new AndroidPackage(pkgname);
                mPackages.Add(npkg);
                var args = new PackageDownloadedEventArgs();
                args.Package = npkg;
                PackageLoaded?.Invoke(this, args);
                return;
            }
            using (var wc = new WebClient())
            {
                wc.DownloadFile(uri, fileName);
                var npkg = new AndroidPackage(pkgname);
                mPackages.Add(npkg);
                var args = new PackageDownloadedEventArgs();
                args.Package = npkg;
                PackageDownloaded?.Invoke(this, args);
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

        /**
         *
         */
        public async Task AddAllAsync(string[] names)
        {
            var tasks = new List<Task>();
            foreach (var name in names)
            {
                var t = Task.Factory.StartNew(() => { AddPackage(name); });
                tasks.Add(t);
            }
            await Task.WhenAll(tasks);
        }
    }
}
