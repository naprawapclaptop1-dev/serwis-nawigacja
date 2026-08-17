using System;
using System.IO;
using System.Linq;
using System.Drawing;
using System.Reflection;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Net.Http;
using System.Text.Json;

namespace DiskRecoveryPRO;

public class MainForm : Form
{
    const string APP_VERSION = "1.5.0";

    readonly Label updateStatus = new();
    readonly Label licenseStatus = new();
    // Disk Recovery PRO source for automated GitHub build.
