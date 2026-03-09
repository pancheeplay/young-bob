## 编译与检查

- 当前环境下，常常不能使用 Unity batch compile，因为项目可能正被编辑器占用。
- 优先使用：

```bash
dotnet build Assembly-CSharp.csproj -nologo
```