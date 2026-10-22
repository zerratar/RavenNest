using System;
using System.Collections.Generic;
using System.Data.SqlClient;

namespace RavenNest.BusinessLogic.Data
{
    /// <summary>One parameterized statement batch of a save.</summary>
    public sealed class SqlSaveCommand
    {
        public SqlSaveCommand(string text, List<SqlParameter> parameters)
        {
            Text = text;
            Parameters = parameters;
        }

        public string Text { get; }
        public List<SqlParameter> Parameters { get; }
    }

    public class SqlSaveQuery
    {
        private readonly Func<string> describe;
        private string command;

        public SqlSaveQuery(string command)
        {
            this.command = command;
            Commands = string.IsNullOrEmpty(command)
                ? Array.Empty<SqlSaveCommand>()
                : new[] { new SqlSaveCommand(command, new List<SqlParameter>()) };
        }

        /// <param name="commands">What is run, in order.</param>
        /// <param name="describe">The same save with its values written out, made only when it is logged.</param>
        public SqlSaveQuery(IReadOnlyList<SqlSaveCommand> commands, Func<string> describe)
        {
            Commands = commands;
            this.describe = describe;
        }

        public IReadOnlyList<SqlSaveCommand> Commands { get; }

        /// <summary>The save as plain SQL with its values, for the logs and the error query file.</summary>
        public string Command => command ??= describe?.Invoke();
    }
}
