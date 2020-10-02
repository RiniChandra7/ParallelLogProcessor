# ParallelLogProcessor
This application aims to extract log entries within a particular time range, supplied by the user in the format shown. The output is a folder containing all the required log entries, while the input is given in the format shown in the screenshot, with the path being that of the full archive. All time formats are in ISO 8601 format. The aim was to minimize the waiting time of the user considering the the total log size is in the order of TBs (Test data of that size was unavailable, but testing was done for upto a thousand log files with a thousand log entries each). This approach use parallel programming in C#, and successfully minimized the total execution time to half (as compared to sequential approach), as observed in the testing done so far.

## How to use-
The input format needs to be as shown below. Most invalid input conditions have been handled in the code to avoid wasting execution time.
![Input format](https://github.com/RiniChandra7/ParallelLogProcessor/blob/master/log.jpg?raw=true)
In the above case, the first output log file containing 1000 lines was ready in the time shown above, which means this implementation has so far been able to start generating input within 1-2 seconds.
The command is supposed to be executed in the same format as shown above.
The executable file LogExtractor.exe can be found inside the project folder at LogExtractor\LogExtractor\bin\Debug\LogExtractor.exe.

## Assumptions-
1. It has been mentioned in the problem statement that lines are written sequentially into a file and soon as it reaches a
particular size, a new file is created. Since time always moves in forward direction, it is safe to say that the lines in
every log file, filled one by one, are filled in an increasing (non-decreasing, to be precise) fashion.
2. Going by point 1, the timestamps in earlier files are smaller than latter ones. Hence, the timestamps from the 1st line
of the 1st file to the last line of the last line are in a sorted order.
3. Since we have a large amount of data to process, it can be safely assumed that the processing would happen on a
modern day device, which is highly likely to support parallelization, which is at the core of my implementation.
4. For a single log file, duplicate files are completely unlikely to exist because a timestamp gone by cannot occur again.
5. A particular timestamp can appear a number of times across the logs because it is possible for multiple actions to
have taken place at the same time.

## Focus- 
The focus of research and implementation has been to stick to the first principles & good practices of
programming. Since the exact data for which this implementation has been built is unavailable, I was supposed to come
up with an efficient way of handling data shows potential of being suitable for the actual data too. Hence, I worked to
find the most efficient way of programmatically handling it without compromising on the ease of usage. During my
research, I looked up how to maximize the potential of the multi-core modern day CPUs, and came across the concept of
parallelization, which is best when the amount of data being handled is large. I also focused on handling timestamps
properly to avoid bugs caused to time-zone differences, duplicity, etc. I also tried to use most efficient ways of matching
wherever required, for example, regular expressions. Also, the most optimized algorithms were chosen to the best of my
knowledge. I’ve also attempted to optimize any potential blockers or bottlenecks, by using remedial code – for instance,
if parallel for breaks before finding the real earliest occurrence.

## Implementation (in C#)- 
I have used an approach where I have parallelized data processing wherever possible, which has
greatly improved the efficiency as compared to the sequential implementation. A rough observation that was constant
among all test cases was that that it takes at most less than half the time as it would in the sequential implementation.
The first principles of programming, for instance, writing modular, legible and well-documented code (lucid
comments have been added) – have been kept in mind, after a brief research on the same. I’ve optimized the code to the
best of my knowledge and also handled the corner cases that I could think of. Detailed validations are done on the input
beforehand so that computation time is not wasted on invalid data.
I’ve also tried to give attention to the details and look at the project from the user’s perspective. My approach
attempts to provide a streamlined, traceable and lucid output. The implementation steps can be summed up as follows:
1. Input is given strictly in the format specified in the problem statement. Validations are processed on the given input
to ensure the same.
2. If the given input is valid, we check if the input directory contains log files. We go ahead only if it does, and the files
are named in the specified manner.
3. Then we locate the file in which the earliest occurrence of the From timestamp is found. If the From timestamp is
earlier than the earliest timestamp in the archive, then its value is reset to the earliest available timestamp.
4. Then we locate the file in which the last occurrence of the To timestamp is found. If the To timestamp is latter than
the latest timestamp in the archive, then its value is reset to the latest available timestamp.
5. The exact lines containing the above two timestamps is found using Binary search. The earliest occurrence is taken
for the From timestamp & the latest for To.
6. All the lines from the start location to the end location, including those found in the files lying in between; constitute
the output we need.
7. The output directory is created in the same directory as the parent directory of the input archives & is opened,
bearing the current timestamp (i.e. when the processing ended) so that the output folder name is unique every time.

## Observations- 
Up to 1000 sample log files bearing at least 1000 lines each have been used to test this implementation. It
was observed that for a very small number of log files (e.g. 1-100 in the testing), certain parts of the code worked faster
using sequential For. But since it has been given that we are processing a large number, the parallel approach offering
significant speedups was preferred, and output creation always began well within 1-2 seconds in the testing, which was
done on a dual core machine. The implementation also works if we provide a timestamp that does not have an exact
match but lies between 2 available timestamps.
