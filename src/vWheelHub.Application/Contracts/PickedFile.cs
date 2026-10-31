using System;
using System.IO;
using System.Threading.Tasks;

namespace SimRacingHub.Application.Contracts
{
    /// <summary>
    /// Neutral representation of a file selected through the file picker.
    /// Provides stream access without leaking platform-specific file handles.
    /// </summary>
    public sealed class PickedFile
    {
        public string FileName { get; init; } = string.Empty;
        public string? LocalFilePath { get; init; }
        public Func<Task<Stream>> OpenReadAsync { get; init; } = () => Task.FromResult<Stream>(Stream.Null);
        public Func<Task<Stream>> OpenWriteAsync { get; init; } = () => Task.FromResult<Stream>(Stream.Null);

        public static PickedFile FromBytes(string fileName, byte[] data, string? localFilePath = null)
        {
            return new PickedFile
            {
                FileName = fileName,
                LocalFilePath = localFilePath,
                OpenReadAsync = () => Task.FromResult<Stream>(new MemoryStream(data)),
                OpenWriteAsync = () => Task.FromResult<Stream>(new MemoryStream())
            };
        }

        public static PickedFile FromFile(string filePath)
        {
            var fileName = Path.GetFileName(filePath);
            return new PickedFile
            {
                FileName = fileName,
                LocalFilePath = filePath,
                OpenReadAsync = () => Task.FromResult<Stream>(File.OpenRead(filePath)),
                OpenWriteAsync = () => Task.FromResult<Stream>(File.Create(filePath))
            };
        }
    }
}
