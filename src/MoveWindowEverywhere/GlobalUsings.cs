// 项目级 using。WPF / WinForms 同时启用时隐式 using 并不包含 System.IO 等命名空间，
// 这里统一补齐；不引入 System.Windows.Forms / System.Windows 的全局 using，避免类型歧义。
global using System.Diagnostics;
global using System.IO;
global using System.Threading;
