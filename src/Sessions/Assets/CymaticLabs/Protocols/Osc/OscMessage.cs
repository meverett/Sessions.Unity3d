using System.Text;
using System.Net;

namespace CymaticLabs.Protocols.Osc
{
    /// <summary>
    /// A basic OSC message.
    /// </summary>
    public class OscMessage
    {
        /// <summary>
        /// Whether or not this OSC message has been marked "reliable".
        /// </summary>
        internal bool Reliable = false;

        /// <summary>
        /// Whether or not this OSC message has been marked "no broadcast".
        /// </summary>
        internal bool NoBroadcast = false;

        /// <summary>
        /// Gets the address of the message.
        /// </summary>
        public string Address { get; private set; }

        /// <summary>
        /// Gets the message's arguments.
        /// </summary>
        public object[] Arguments { get; private set; }

        /// <summary>
        /// The number of message arguments.
        /// </summary>
        public int Count { get;  private set; }

        /// <summary>
        /// The message's origin IP end point.
        /// </summary>
        public IPEndPoint Origin { get; private set; }

        /// <summary>
        /// The size of the OSC message in bytes.
        /// </summary>
        public int SizeInBytes { get; private set; }

        /// <summary>
        /// Whether or not the message is empty.
        /// </summary>
        public bool IsEmpty { get; private set; }

        /// <summary>
        /// The underlying raw message object, if any.
        /// </summary>
        public object Raw { get; private set; }

        /// <summary>
        /// Creates an OSC message with the given address and arguments.
        /// </summary>
        /// <param name="address">The message's address.</param>
        /// <param name="arguments">The message's argument values.</param>
        /// <param name="isEmpty">Whether or not the message is empty.</param>
        /// <param name="count">The number of arguments in the message.</param>
        /// <param name="origin">The source IP end point where the message originated.</param>
        /// <param name="sizeInBytes">The size of the message in bytes.</param>
        /// <param name="raw">The underlying raw message object, if any.</param>
        public OscMessage(string address, object[] arguments, bool isEmpty, int count, IPEndPoint origin, int sizeInBytes, object raw = null)
        {
            Address = address;
            Arguments = arguments;
            IsEmpty = isEmpty;
            Count = count;
            Origin = origin;
            SizeInBytes = sizeInBytes;
            Raw = raw;
        }

        /// <summary>
        /// Overriden ToString to return message details.
        /// </summary>
        /// <returns>Returns the contents of the message as a string.</returns>
        public override string ToString()
        {
            StringBuilder sb = new StringBuilder();
            sb.Append(Address);
            sb.Append(" => ");

            if (Arguments != null)
            {
                for (int i = 0; i < Arguments.Length; i++)
                {
                    sb.AppendFormat("{0}", Arguments[i]);
                    if (i < Arguments.Length - 1) sb.Append(", ");
                }
            }

            return sb.ToString();
        }
    }
}
