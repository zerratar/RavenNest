namespace UnityVersionChanger
{
    public class BinaryFile : IDisposable
    {
        protected readonly string File;
        protected int DataPosition;
        protected byte[] Data;
        protected bool Modified;

        public BinaryFile(string file)
        {
            this.File = file;
            this.Data = System.IO.File.ReadAllBytes(file);
        }

        public int Size
        {
            get => Data.Length;
        }

        public int Position
        {
            get => DataPosition;
            set => DataPosition = value;
        }

        public void WriteString(string value)
        {
            WriteString(DataPosition, value, value.Length, '\0');
        }

        public void WriteString(string value, int length, char padChar)
        {
            WriteString(DataPosition, value, length, padChar);
        }

        public void WriteString(long offset, string value, int length, char padChar)
        {
            if (offset < 0 || length < 0 || length > Data.Length || offset + length > Data.Length)
                return;

            Modified = length > 0;
            for (var i = 0; i < length; ++i)
            {
                if (i < value.Length)
                {
                    Data[offset + i] = (byte)value[i];
                }
                else
                {
                    Data[offset + i] = (byte)padChar;
                }
            }
        }

        public int IndexOf(string text)
        {
            return IndexOf(text, DataPosition, StringComparison.Ordinal);
        }

        public int IndexOf(string text, StringComparison comparison)
        {
            return IndexOf(text, DataPosition, comparison);
        }

        public int IndexOf(string text, int startIndex)
        {
            return IndexOf(text, startIndex, StringComparison.Ordinal);
        }

        public int IndexOf(string text, int startIndex, StringComparison comparison)
        {
            var characters = text.ToCharArray();
            var index = startIndex;
            while (index < Data.Length)
            {
                var at = index;
                var c = (char)Data[index];
                if (c == characters[0])
                {
                    if (comparison == StringComparison.Ordinal)
                    {
                        var wasFound = true;
                        for (var i = 1; i < characters.Length; i++)
                        {
                            c = (char)Data[index + i];
                            if (c != characters[i])
                            {
                                wasFound = false;
                                break;
                            }
                        }
                        if (wasFound)
                        {
                            return at;
                        }
                    }
                    else
                    {
                        var str = c + "";
                        for (var i = 1; i < characters.Length; i++)
                        {
                            str += (char)Data[index + i];
                        }
                        if (string.Compare(text, str, comparison) == 0)
                        {
                            return at;
                        }
                    }
                }
                index++;
            }
            return -1;
        }

        public int ReadStringLength()
        {
            var endOfStringEncountered = false;
            int len = 0;
            do
            {
                len++;
                var value = (char)Data[DataPosition++];
                if (value == '\0')
                {
                    endOfStringEncountered = true;
                    continue;
                }
                else if (endOfStringEncountered)
                    return len;
            } while (DataPosition < Data.Length);
            return -1;
        }

        public string ReadString()
        {
            var str = "";
            do
            {
                var value = (char)Data[DataPosition++];
                if (value == '\0')
                    break;

                str += value;
            } while (DataPosition < Data.Length);
            return str;
        }

        public void Dispose()
        {
            Save(false);
        }

        public void Save(bool createBackup = false)
        {
            if (!Modified)
            {
                return;
            }

            if (createBackup)
            {
                System.IO.File.Copy(File, File + ".bak", true);
            }

            System.IO.File.WriteAllBytes(File, Data);
            Modified = false;
        }
    }
}
