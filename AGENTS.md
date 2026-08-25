# WindowTool 开发约定

## 构建

```bat
build.bat
```

- 编译器：`%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe`（只支持 C# 5 语法：
  禁止字符串插值 `$""`、`?.`、模式匹配 `is T x`、表达式体成员等新语法）
- 引用系统自带 WPF 程序集（视频背景用），清单 app.manifest 提供 DPI 感知
- 构建前若 WindowTool.exe 被占用，先结束运行中的 WindowTool 进程

## 打包（每次改动后必须执行）

1. `build.bat` 构建通过
2. 用 PowerShell 压缩为 zip，放在**项目根目录**，供上传 GitHub Release：

```powershell
Compress-Archive -Force -Path WindowTool.exe,使用说明.txt,README.md -DestinationPath WindowTool_v<版本>.zip
```

- 版本号递增：v1.4 → v1.5 → v1.8 …（改功能就递增）
- `WindowTool_v*.zip` 已加入 .gitignore，zip 不入库

## 其他

- 每次功能改动需同步更新 README.md 与 使用说明.txt
- 提交信息用英文，一行主题 + 要点列表
- **只提交不推送**：除非用户明确说"推送/push"，绝不执行 `git push`
- 本机 git 完整路径：`C:\Program Files\Git\cmd\git.exe`（不在 shell 默认 PATH 里）
- 远程仓库：https://github.com/chenmoulaile/WindowTool.git
