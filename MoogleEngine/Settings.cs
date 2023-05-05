using System;
using System.IO;
using System.Reflection;

namespace MoogleEngine;

class Settings {

    static public DirectoryInfo BASE_DIR = new DirectoryInfo(
        Assembly.GetAssembly(typeof (Moogle)).Location
    ).Parent.Parent.Parent.Parent.Parent;
    static public DirectoryInfo DATA_DIR = new DirectoryInfo(Path.Join(BASE_DIR.ToString(), "Content"));

}
