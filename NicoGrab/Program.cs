using NicoLib;
using System.Threading.Tasks.Dataflow;

public static class Program
{
    readonly static string[] extraFiles = [
        "XAB", "XAC", "XAD", "XAE"
    ];

    public static int Main(string[] args)
    {
        if (args.Length < 2)
        {
            Console.Error.WriteLine("NO ARGS.");
            return -1;
        }

        string indexPath = args[0];
        string dataPath = args[1];
        string outFolder = Environment.CurrentDirectory;
        if (args.Length > 2)
            outFolder = args[2];

        if (!Directory.Exists(outFolder))
            Directory.CreateDirectory(outFolder);

        if (!File.Exists(indexPath))
        {
            Console.Error.WriteLine("Noo Index.");
            return -1;
        }

        //List<FileStream> streams = new List<FileStream>();
        if (!File.Exists(dataPath))
        {
            Console.WriteLine("No Data file.");
            return -1;
        }
        //streams.Add(File.OpenRead(dataPath));
        
        /*foreach (var extra in extraFiles)
        {
            dataPath = Path.GetDirectoryName(dataPath);
            dataPath = Path.Combine(dataPath, extra);
            if (!File.Exists(dataPath))
            {
                Console.WriteLine($"Missing extra file {extra}");
                foreach (var stream in streams)
                    stream.Dispose();
                return -1;
            }
            streams.Add(File.OpenRead(dataPath));
        }*/

        using StreamReader indexReader = new StreamReader(indexPath);
        using FileStream dataFile = File.OpenRead(dataPath);
        //using MultiStream dataFile = new MultiStream(streams);

        string? currentLine;
        int lineCount = 0;
        while ((currentLine = indexReader.ReadLine()) != null)
        {
            string[] parts = currentLine.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            
            if (parts.Length != 3)
            {
                Console.Error.WriteLine($"Bad Read [{lineCount}]: {currentLine}");
                return -1;
            }
            
            lineCount++;
            
            long offset = long.Parse(parts[0], System.Globalization.NumberStyles.HexNumber);
            long size = long.Parse(parts[1]);
            string name = parts[2];
            
            string targetPath = Path.Combine(outFolder, name);
            Directory.CreateDirectory(Path.GetDirectoryName(targetPath));
            using FileStream targetFile = File.Open(targetPath, FileMode.Create, FileAccess.Write);
            
            byte[] buffer = new byte[size];
            dataFile.Seek(offset, SeekOrigin.Begin);
            dataFile.ReadExactly(buffer);
            targetFile.Write(buffer);
            Console.WriteLine(targetPath);
        }


        return 0;
    }
}