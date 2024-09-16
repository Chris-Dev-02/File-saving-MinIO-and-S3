using System;
using System.Collections.Generic;

namespace File_saving.Models;

public partial class File
{
    public int? Id { get; set; }

    public byte[]? FileDb { get; set; }

    public string? PathFile { get; set; }
}
