﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RePKG.WPF.Loggers
{
    public interface ILogger
    {
        public LogLevel Level { get; }

        public void Log(LogLevel level, string message);
        public void Log(string message);

        public void Debug(string message);

        public void Info(string message);

        public void Waining(string message);

        public void Error(string message);

        /// <summary>
        /// 进度
        /// </summary>
        /// <param name="current"></param>
        /// <param name="total"></param>
        public void Progress(long current, long total);

        public void Progress(long current, long total, string message);

    }
}
