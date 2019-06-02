﻿using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using Arc.YTSubConverter.Formats.Ass;
using Arc.YTSubConverter.Util;

namespace Arc.YTSubConverter.Formats
{
    internal abstract class SubtitleDocument
    {
        public static readonly DateTime TimeBase = new DateTime(2000, 1, 1);

        protected SubtitleDocument()
        {
        }

        protected SubtitleDocument(SubtitleDocument doc)
        {
            VideoDimensions = doc.VideoDimensions;
            Lines.AddRange(doc.Lines);
        }

        public Size VideoDimensions
        {
            get;
            set;
        }

        public List<Line> Lines { get; } = new List<Line>();

        public static SubtitleDocument Load(string filePath)
        {
            switch (Path.GetExtension(filePath)?.ToLower())
            {
                case ".ass":
                    return new AssDocument(filePath);

                case ".sbv":
                    return new SbvDocument(filePath);

                case ".srt":
                    return new SrtDocument(filePath);

                case ".ytt":
                    return new YttDocument(filePath);

                default:
                    throw new NotSupportedException();
            }
        }

        public void MergeSimultaneousLines()
        {
            List<Line> lines = Lines.OrderBy(l => l.Start).ToList();     // Use OrderBy to get a stable sort (List.Sort() is unstable)

            int i = 0;
            while (i < lines.Count)
            {
                if (lines[i].Position != null)
                {
                    i++;
                    continue;
                }

                Line firstLine = lines[i];
                Line secondLine = null;

                int j = i + 1;
                while (j < lines.Count && lines[j].Start < lines[i].End)
                {
                    if (lines[j].Position == null && lines[j].AnchorPoint == firstLine.AnchorPoint)
                    {
                        secondLine = lines[j];
                        break;
                    }
                    j++;
                }

                if (secondLine == null)
                {
                    i++;
                    continue;
                }

                lines.RemoveAt(j);
                lines.RemoveAt(i);

                if (firstLine.Start < secondLine.Start)
                    InsertConcatenedLine(lines, i, firstLine.Start, secondLine.Start, firstLine);

                if (AnchorPointUtil.IsBottomAligned(firstLine.AnchorPoint))
                    InsertConcatenedLine(lines, i, secondLine.Start, TimeUtil.Min(firstLine.End, secondLine.End), secondLine, firstLine);
                else
                    InsertConcatenedLine(lines, i, secondLine.Start, TimeUtil.Min(firstLine.End, secondLine.End), firstLine, secondLine);

                if (firstLine.End < secondLine.End)
                    InsertConcatenedLine(lines, i, firstLine.End, secondLine.End, secondLine);
                else if (secondLine.End < firstLine.End)
                    InsertConcatenedLine(lines, i, secondLine.End, firstLine.End, firstLine);
            }

            Lines.Clear();
            Lines.AddRange(lines);
        }

        private static void InsertConcatenedLine(List<Line> targetList, int baseIndex, DateTime start, DateTime end, params Line[] sourceLines)
        {
            Line line = (Line)sourceLines[0].Clone();
            for (int i = 1; i < sourceLines.Length; i++)
            {
                line.Sections.Last().Text += "\r\n";
                line.Sections.AddRange(sourceLines[i].Sections.Select(s => (Section)s.Clone()));
            }

            line.Start = start;
            line.End = end;

            int index = baseIndex;
            while (index < targetList.Count && targetList[index].Start < start)
            {
                index++;
            }

            targetList.Insert(index, line);
        }

        public void CloseGaps()
        {
            SortedList<DateTime, object> startTimes = new SortedList<DateTime, object>();
            foreach (Line line in Lines)
            {
                startTimes[line.Start] = null;
            }

            foreach (Line line in Lines)
            {
                int endTimeIdx = startTimes.Keys.BinarySearchIndexAtOrAfter(line.End);

                int timeGapBefore = endTimeIdx > 0 ? (int)(line.End - startTimes.Keys[endTimeIdx - 1]).TotalMilliseconds : int.MaxValue;
                int timeGapAfter = endTimeIdx < startTimes.Count ? (int)(startTimes.Keys[endTimeIdx] - line.End).TotalMilliseconds : int.MaxValue;

                if (timeGapBefore < 50 && timeGapBefore < timeGapAfter)
                    endTimeIdx--;
                else if (timeGapAfter < 50 && timeGapAfter <= timeGapBefore)
                    ;
                else
                    continue;

                line.End = startTimes.Keys[endTimeIdx];
            }
        }

        public PointF GetDefaultPosition(AnchorPoint anchorPoint)
        {
            float left = VideoDimensions.Width * 0.02f;
            float center = VideoDimensions.Width / 2.0f;
            float right = VideoDimensions.Width * 0.98f;

            float top = VideoDimensions.Height * 0.02f;
            float middle = VideoDimensions.Height / 2.0f;
            float bottom = VideoDimensions.Height * 0.98f;

            switch (anchorPoint)
            {
                case AnchorPoint.TopLeft:
                    return new PointF(left, top);

                case AnchorPoint.TopCenter:
                    return new PointF(center, top);

                case AnchorPoint.TopRight:
                    return new PointF(right, top);

                case AnchorPoint.MiddleLeft:
                    return new PointF(left, middle);

                case AnchorPoint.Center:
                    return new PointF(center, middle);

                case AnchorPoint.MiddleRight:
                    return new PointF(right, middle);

                case AnchorPoint.BottomLeft:
                    return new PointF(left, bottom);

                case AnchorPoint.BottomCenter:
                    return new PointF(center, bottom);

                case AnchorPoint.BottomRight:
                    return new PointF(right, bottom);

                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        public virtual void Save(string filePath)
        {
            throw new NotImplementedException();
        }
    }
}
