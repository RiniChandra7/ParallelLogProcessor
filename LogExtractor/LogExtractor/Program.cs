using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Text.RegularExpressions;

namespace LogExtractor
{
    class Extract
    {
        static int SearchInfile(string file, string ts)
        {
            /*
             * This is the function that helps us locate if the given timestamp lies in the current file.
             * The idea is that since time moves only in the forward direction, and we're working with time based logs, there should only be one file where the start or end timestamp can be correctly located.
             * A timestamp TS exists in a file if (Lowest timestamp in file)<=TS<=(Highest timestamp in file).
            */
            string[] lines = File.ReadAllLines(file);
            //Each line has comma-separated values, where the first value is the timestamp as specified in the requirements.
            string lineDate = lines[0].Substring(0, lines[0].IndexOf(','));
            DateTime lowDate = DateTime.Parse(lineDate).ToUniversalTime();
            lineDate = lines[lines.Length - 1].Substring(0, lines[lines.Length - 1].IndexOf(','));
            DateTime highDate = DateTime.Parse(lineDate).ToUniversalTime();
            DateTime searchDate = DateTime.Parse(ts).ToUniversalTime();
            //If the search timestamp lies between the lowest & highest timestamps in the file, we return 1 else 0.
            if (DateTime.Compare(lowDate, searchDate) <= 0 && DateTime.Compare(searchDate, highDate) <= 0)
            {
                return 1;
            }
            else
            {
                return 0;
            }
        }

        static int SearchTS(string file, string ts, char lim)
        {
            /*
                * This function is used to locate the starting or ending point in the file that we found through SearchInFile function.
                * This assumes that since time moves in forward direction, the first to last timestamp in a file are in increasing order.
                * The basic idea used is of binary search.
                * If a timestamp occurs multiple times, 
                    * The lowest matching index is returned if we're talking about the starting point.
                    * The highest matching index is returned if we're talking about the ending point.
                * If an exact match is not found,
                    * The index of the timestamp just greater than it is returned for the starting point.
                    * The index of the timestamp just lower than it is returned for the ending point.
                * The lim argument determines if we're searching for the lower limit or the upper limit.
            */
            string[] lines = File.ReadAllLines(file);
            int begin = 0;
            int end = lines.Length - 1;
            DateTime searchDate = DateTime.Parse(ts).ToUniversalTime();
            int mid = 0;

            while (begin <= end)
            {
                mid = (begin + end) / 2;
                string lineDate = lines[mid].Substring(0, lines[mid].IndexOf(','));
                DateTime midDate = DateTime.Parse(lineDate).ToUniversalTime();

                if (DateTime.Compare(searchDate, midDate) == 0)
                {
                    //This 'if' ensures that when we're looking for lower limit, the least matching index is returned.
                    if (lim == 'l' || lim == 'L')
                    {
                        //This loop terminates as soon as a timestamp smaller than the required one is found.
                        while (DateTime.Compare(searchDate, midDate) == 0 && mid > 0)
                        {
                            mid = mid - 1;
                            lineDate = lines[mid].Substring(0, lines[mid].IndexOf(','));
                            midDate = DateTime.Parse(lineDate).ToUniversalTime();
                        }
                        //The last value of mid will be 1 less than required. Hence, returning mid + 1.
                        return mid + 1;
                    }
                    //This 'if' ensures that when we're looking for the upper limit, the highest matching index is returned.
                    if (lim == 'h' || lim == 'H')
                    {
                        //This loop terminates as soon as a timestamp higher than the required one is found.
                        while (DateTime.Compare(searchDate, midDate) == 0 && mid < lines.Length - 1)
                        {
                            mid = mid + 1;
                            lineDate = lines[mid].Substring(0, lines[mid].IndexOf(','));
                            midDate = DateTime.Parse(lineDate).ToUniversalTime();
                        }
                        //The last value of mid will be 1 more than required. Hence, returning mid - 1.
                        return mid - 1;
                    }
                    
                    //This is just for syntactical correctness; this is never practically used.
                    return mid;
                }
                else if (DateTime.Compare(searchDate, midDate) < 0)
                {
                    //To reduce search to left half of the array
                    end = mid - 1;
                }
                else
                {
                    //To reduce search to right half of the array
                    begin = mid + 1;
                }
            }

            //The next few conditions are for situations where the exact matching timestamp is not found.
            if (lim == 'l' || lim == 'L')
            {
                //For the lower limit, it should either be at the end index or its successor.
                string lineDate = lines[end].Substring(0, lines[end].IndexOf(','));
                DateTime endDate = DateTime.Parse(lineDate).ToUniversalTime();
                if (DateTime.Compare(searchDate, endDate) < 0)
                {
                    return end;
                }
                else
                {
                    return end + 1;
                }
            }
            if (lim == 'h' || lim == 'H')
            {
                //For the upper limit, it should either be at the begin index or its predecessor.
                string lineDate = lines[begin].Substring(0, lines[begin].IndexOf(','));
                DateTime beginDate = DateTime.Parse(lineDate).ToUniversalTime();
                if (DateTime.Compare(searchDate, beginDate) > 0)
                {
                    return begin;
                }
                else
                {
                    return begin - 1;
                }
            }
            return 0;
        }

        static int FileLocator(string[] files, string ts)
        {
            //This function returns the index of the file containing the required timestamp
            int fileNum = -1;

            //This Parallel.For aims to locate the file with the starting point.
            Parallel.For(0, files.Length, (ind, stopper) =>
            {
                //Use SearchInFile function to locate the file with the starting point based on a simple if condition.
                if (SearchInfile(Path.GetFullPath(files[ind]), ts) == 1)
                {
                    stopper.Break();
                    fileNum = (int)stopper.LowestBreakIteration;
                    //The for loop is used in cases where a timestamp appearing many times is spread across files.
                    //Need to check only those files which come before the current value of startFile.
                    //Useful if parallel for breaks before finding the real start point on rare occasions.
                    int dupStart = fileNum;
                    for (int i = 1; i <= fileNum; i++)
                    {
                        if (SearchInfile(Path.GetFullPath(files[fileNum - i]), ts) == 1)
                        {
                            dupStart--;
                        }
                        else
                        {
                            //Mostly, the file just before the current startFile does not contain the timestamp. Hence, the loop breaks after just 1 iteration.
                            break;
                        }
                    }
                    if (dupStart != fileNum)
                    {
                        fileNum = dupStart;
                    }
                }
            });

            return fileNum;
        }

        static DirectoryInfo CreateOutDirectory(string currentFile)
        {
            /*
                * This function is responsible for making the output directory.
                * This function creates an output directory where timestamps lying within the given range are written with the same names as input log files.
                * The output directory is created in the parent directory of the input directory path, for better coherence.
                * The file name bears timestamps so that the output log folder name is unique everytime.
            */
            DirectoryInfo currentParent = Directory.GetParent(currentFile);
            DirectoryInfo reqParent = currentParent.Parent;
            string outputPath = reqParent.Name + "\\OutputLogs-" + DateTime.Now.ToString("yyyy-MM-ddTHH-mm-ss-fffZ");
            DirectoryInfo output = Directory.CreateDirectory(outputPath);
            //The output folder path is printed onto the console for the ease of the user.
            Console.WriteLine("Check output logs at " + output.FullName);
            return output;
        }

        static void MakeStartFile(string filePath, string ts, string outFolder)
        {
            /*
             * A utility function that prints the start file from the correct starting point onwards, till the end of the file.
             * Useful when the start & end points are located in different files.
             */
            //Get the line with the correct starting point.
            int loc = SearchTS(Path.GetFullPath(filePath), ts, 'l');
            //All lines before the starting point are skipped and remaining are taken.
            string[] startLines = File.ReadLines(Path.GetFullPath(filePath)).Skip(loc).ToArray();
            //A new file with the same name as the input log file being considered is created in which the required data is written.
            string startFileName = filePath.Substring(filePath.LastIndexOf('\\'));
            string newFile = outFolder + "\\" + startFileName;
            System.IO.File.WriteAllLines(newFile, startLines);
        }

        static void MakeEndFile(string filePath, string ts, string outFolder)
        {
            /*
                * A utility function that prints the end file till the correct ending point, from the top of the file.
                * Useful when the start & end points are located in different files.
            */
            //Get the line with the correct ending point.
            int loc = SearchTS(Path.GetFullPath(filePath), ts, 'h');
            //All lines after the ending point are skipped.
            string[] endLines = File.ReadLines(Path.GetFullPath(filePath)).Take(loc + 1).ToArray();
            string endFileName = filePath.Substring(filePath.LastIndexOf('\\'));
            string newFile = outFolder + "\\" + endFileName;
            System.IO.File.WriteAllLines(newFile, endLines);
        }

        static void MakeFullFile(string filePath, string outFolder)
        {
            /*
                * A utility function that generates the current file as is, from top to bottom.
                * Basically generates a copy.
            */
            //We need all the lines in the file in this case. Hence, location calculation is not needed.
            string[] midLines = File.ReadLines(Path.GetFullPath(filePath)).ToArray();
            string fileName = filePath.Substring(filePath.LastIndexOf('\\'));
            string newFile = outFolder + "\\" + fileName;
            //Using WriteAllLines here instead of copying because both files are of the same name.
            System.IO.File.WriteAllLines(newFile, midLines);
        }

        static void MakeOutput(string[] files, int startFile, int endFile, string ts1, string ts2)
        {
            /*
                * This function is responsible for building the output.
                * Someone using the LogExtractor may also be interested in knowing which timestamps came from which log file.
                * We should also ensure that file size limits are respected (for example, 16GB as specified).
                * This can be ensured if each log file is no bigger than its corresponding input log file.
                * Thus, this function creates an output directory where timestamps lying within the given range are written with the same names as input log files.
                * This function makes use of Parallel.for to leverage parallel processing capabilities of modern day processors.
            */
            try
            {
                //The following 'if' is for the case where the startFile comes before the endFile, and both files are different.
                if (startFile != -1 && endFile != -1 && startFile < endFile)
                {
                    DirectoryInfo output = CreateOutDirectory(files[startFile]);

                    //The Parallel.for loop helps achieve parallel writing of log files for faster processing.
                    Parallel.For(startFile, (endFile + 1), ind =>
                    {
                        //For the startFile, we need all lines from the starting point till the end.
                        if (ind == startFile)
                        {
                            MakeStartFile(files[startFile], ts1, output.FullName);
                            watch.Stop();
                            Console.WriteLine("First file ready after " + watch.ElapsedMilliseconds + " ms.");
                        }
                        //For the endFile, we need all lines from the top till the ending point.
                        else if (ind == endFile)
                        {
                            MakeEndFile(files[endFile], ts2, output.FullName);
                        }
                        //For all other files in between, we need the entire file.
                        else
                        {
                            MakeFullFile(files[ind], output.FullName);
                        }
                    });
                    //Automatically open the output folder once done.
                    System.Diagnostics.Process.Start(output.FullName);
                }
                //This is the case where the starting & ending point occurs in the same file.
                else if (startFile != -1 && endFile != -1 && startFile == endFile)
                {
                    int loc1 = SearchTS(Path.GetFullPath(files[startFile]), ts1, 'l');
                    int loc2 = SearchTS(Path.GetFullPath(files[endFile]), ts2, 'h');
                    DirectoryInfo output = CreateOutDirectory(files[startFile]);
                    //All lines between the start & end point are taken.
                    string[] midLines = File.ReadLines(Path.GetFullPath(files[endFile])).Take(loc2 + 1).Skip(loc1).ToArray();
                    string midFileName = files[endFile].Substring(files[endFile].LastIndexOf('\\'));
                    string newFile = output.FullName + "\\" + midFileName;
                    System.IO.File.WriteAllLines(newFile, midLines);
                    watch.Stop();
                    Console.WriteLine("First file ready after " + watch.ElapsedMilliseconds + " ms.");
                    System.Diagnostics.Process.Start(output.FullName);
                }
                //If the from timestamp is less than the the least timestamp in the archives, the least available timestamp is considered the start point.
                else if (startFile == -1 && endFile != -1)
                {
                    Console.WriteLine("From timestamp is less than the least available timestamp in the archives. Generating logs from earliest available timestamp...");
                    DirectoryInfo output = CreateOutDirectory(files[0]);
                    //Since we need the earliest available timestamp, the loop below runs from the first available file as it will have the earliest logs.
                    Parallel.For(0, (endFile + 1), ind =>
                    {
                        if (ind == endFile)
                        {
                            MakeEndFile(files[endFile], ts2, output.FullName);
                        }
                        else
                        {
                            MakeFullFile(files[ind], output.FullName);
                        }
                    });
                    System.Diagnostics.Process.Start(output.FullName);
                }
                //If the To timestamp is greater than the latest available timestamp, we reset it to the latest timestamp in the archives.
                else if (startFile != -1 && endFile == -1)
                {
                    Console.WriteLine("To timestamp is greater than the greatest available timestamp in the archives. Generating logs till the latest available timestamp...");
                    DirectoryInfo output = CreateOutDirectory(files[startFile]);
                    //Since we'll need the latest timestamp, the loop below runs till the last log file.
                    Parallel.For(startFile, files.Length, ind =>
                    {
                        if (ind == startFile)
                        {
                            MakeStartFile(files[startFile], ts1, output.FullName);
                            watch.Stop();
                            Console.WriteLine("First file ready after " + watch.ElapsedMilliseconds + " ms.");
                        }
                        else
                        {
                            MakeFullFile(files[ind], output.FullName);
                        }
                    });
                    System.Diagnostics.Process.Start(output.FullName);
                }
                //The last case is where the given range accomodates the entire archive.
                else
                {
                    Console.WriteLine("The given range is too large; the entire archive fits within this range. Generating the output logs is not recommended as it would be exactly the same as the archives. Do you still want to generate output logs anyway? Y/N ");
                    char choice = Convert.ToChar(Console.Read());
                    //Confirmation is sought because the entire archives would be a huge amount of data.
                    //In case the user absolutely needs the output logs anyway, he/she could choose 'Y'.
                    if (choice == 'y' || choice == 'Y')
                    {
                        DirectoryInfo output = CreateOutDirectory(files[0]);
                        Parallel.For(0, files.Length, ind =>
                        {
                            MakeFullFile(files[ind], output.FullName);
                            if (ind == 0)
                            {
                                watch.Stop();
                                Console.WriteLine("First file ready after " + watch.ElapsedMilliseconds + " ms.");
                            }
                        });
                        System.Diagnostics.Process.Start(output.FullName);
                    }
                    else if (choice == 'n' || choice == 'N')
                    {
                        Console.WriteLine("Output logs have not been generated. The entire archive lies within the given time range.");
                    }
                    else
                    {
                        Console.WriteLine("Invalid choice. Execution terminated.");
                    }
                }
            }
            catch (Exception e)
            {
                Console.WriteLine("An error occurred. LogExtractor.exe could not write output files.");
            }
        }

        static System.Diagnostics.Stopwatch watch = new System.Diagnostics.Stopwatch();

        static void Main(string[] args)
        {
            //Input validations happen here
            watch.Start();
            //There should be 6 elements in the command line arguments so that it can be of the exact required input format.
            if (args.Length == 6)
            {
                //Check if all 3 required args have been provided in the correct order, and the from timestamp <= to timestamp.
                if (args[0] == "-f" && args[2] == "-t" && args[4] == "-i" && DateTime.Compare(DateTime.Parse(args[1]).ToUniversalTime(), DateTime.Parse(args[3]).ToUniversalTime()) <= 0)
                {
                    //The input date should exactly match the ISO 8601 format, which is determined using the regex below.
                    Regex date = new Regex(@"^(-?(?:[1-9][0-9]*)?[0-9]{4})-(1[0-2]|0[1-9])-(3[01]|0[1-9]|[12][0-9])T(2[0-3]|[01][0-9]):([0-5][0-9]):([0-5][0-9])(\\.[0-9]+)?(.([0-9][0-9]?[0-9]?[0-9]?))?(Z)$");
                    //Check if both the timestamps are in the correct format & the input directory exists.
                    if (date.IsMatch(args[1]) && date.IsMatch(args[3]) && Directory.Exists(args[5]))
                    {
                        //Get only the log files which have been named in the required way.
                        string[] files = Directory.GetFiles(args[5], "LogFile-*.log", SearchOption.AllDirectories);
                        //Check if there are any log files in the specified directory.
                        if (files.Length == 0)
                        {
                            Console.WriteLine("No log files exist in the given input directory.");
                        }
                        else
                        {
                            int startFile = FileLocator(files, args[1]);
                            //Creating a reverse copy in order to find the last occurrence of the To timestamp, using FileLocator.
                            string[] fileRev = new string[files.Length];
                            Array.Copy(files, fileRev, files.Length);
                            Array.Reverse(fileRev);
                            int endFile = FileLocator(fileRev, args[3]);
                            //Re-evaluating the value of endFile, according to the normal (increasing) order of files.
                            if (endFile != -1)
                            {
                                endFile = (fileRev.Length - 1) - endFile;
                            }

                            //Use MakeOuput function with the correct parameters to generate the final output.
                            MakeOutput(files, startFile, endFile, args[1], args[3]);
                            Console.WriteLine("Execution Complete.");
                        }
                    }
                    else
                    {
                        //Inform the user if the From timestamp is in the incorrect format.
                        if (!date.IsMatch(args[1]))
                        {
                            Console.WriteLine("From timestamp is not in the correct format: " + args[1]);
                        }
                        //Inform the user if the To timestamp is in the incorrect format.
                        if (!date.IsMatch(args[3]))
                        {
                            Console.WriteLine("To timestamp is not in the correct format: " + args[3]);
                        }
                        //Inform the user if the input directory does not exist.
                        if (!Directory.Exists(args[5]))
                        {
                            Console.WriteLine("The given directory does not exist: " + args[5]);
                        }
                        Console.WriteLine("Please check the inputs you have provided.");
                        Console.WriteLine("Here is an example for a valid input:");
                        Console.WriteLine("LogExtractor.exe -f 2020-08-22T21:40:47.762Z -t 2020-08-22T21:53:32.620Z -i F:\\TestLogs");
                    }
                }
                else
                {
                    //Inform the user if the From timestamp occurs after the To timestamp.
                    if (DateTime.Compare(DateTime.Parse(args[1]), DateTime.Parse(args[3])) > 0)
                    {
                        Console.WriteLine("The From Timestamp is greater than the To timestamp. You can exchange the two & try again.");
                    }
                    //Inform the user if the From input is missing.
                    if (args[0] != "-f" && !args.Contains("-f"))
                    {
                        Console.WriteLine("Please provide the From time input.");
                    }
                    //Inform the user if the To input is missing.
                    if (args[2] != "-t" && !args.Contains("-t"))
                    {
                        Console.WriteLine("Please provide the To time input.");
                    }
                    //Inform the user if the File input is missing.
                    if (args[4] != "-i" && !args.Contains("-i"))
                    {
                        Console.WriteLine("Please provide the Path input.");
                    }
                    Console.WriteLine("Please check the inputs you have provided.");
                    Console.WriteLine("Here is an example for a valid input:");
                    Console.WriteLine("LogExtractor.exe -f 2020-08-22T21:40:47.762Z -t 2020-08-22T21:53:32.620Z -i F:\\TestLogs");
                }
            }
            else
            {
                //The number of elements provided as input via command line is not 6, as per the requirement.
                Console.WriteLine("Invalid input length. Please ensure that you have provided all the required inputs in the correct order.");
                Console.WriteLine("Here is an example for a valid input:");
                Console.WriteLine("LogExtractor.exe -f 2020-08-22T21:40:47.762Z -t 2020-08-22T21:53:32.620Z -i F:\\TestLogs");
            }
        }
    }
}
