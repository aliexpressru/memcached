namespace Aer.Memcached.Client.Commands.Extensions;

internal static class CommandResultExtensions
{
    /// <summary>
        /// Sets the result Success to false
        /// </summary>
        /// <param name="source">Result to update</param>
        /// <param name="message">Message indicating source of failure</param>
        /// <param name="ex">Exception causing failure</param>
        /// <returns>Updated source</returns>
        public static CommandResult Fail(this CommandResult source, string message, Exception ex = null)
        {
            source.Success = false;
            source.Message = message;
            source.Exception = ex;
            return source;
        }

        /// <summary>
        /// Sets the result Success to true
        /// </summary>
        /// <param name="source">Result to update</param>
        /// <param name="message">Message indicating a possible warning</param>
        /// <returns>Updated source</returns>
        public static CommandResult Pass(this CommandResult source, string message = null)
        {
            source.Success = true;
            source.Message = message;
            return source;
        }

        /// <summary>
        /// Minimizes the depth of InnerResults and maintain status codes
        /// </summary>
        /// <param name="source">Source to update target from</param>
        /// <param name="target">Target result to update</param>
        public static void Combine(this CommandResult source, CommandResult target)
        {
            target.Message = source.Message;
            target.Success = source.Success;
            target.Exception = source.Exception;
            target.StatusCode = source.StatusCode ?? target.StatusCode;
            target.InnerResult = source.InnerResult ?? source;
        }
}