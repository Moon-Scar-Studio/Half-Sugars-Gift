# Half Sugar's Gift

**本插件现已停更并归档，感谢大家一直以来的支持。  
感谢所有开发者以及美术人员对本插件做出的贡献。  
Fin. 2026.5.10 - 2026.10.31**


## Select Language 
> 简体中文——本文档  
> [繁體中文](README_zh-hant.md)  
> [English](README_en.md)  
> [日本語](README_ja.md)  

# 跳转到对应章节
- [正式版本](#正式版本)
- [预发布版本](#预发布版本)
- [安装教程](#导入方法)
- [联系我们](#加入我们的群聊)
- [常见错误解决方式](#常见错误解决方式)

# 正式版本

|HSG版本|对应Among Us版本|下载链接|
|---------|---------|---------|
|S-1.0.1-3|17.3.0+|[下载](https://github.com/hvtXsvc-skysilk/Half-sugar-s-gift/releases/download/S-1.0.1-2/Half.sugar.s.gift.S-1.0.1-3.zip)|
|S-1.0.1-1|17.3.0+|[下载](https://github.com/hvtXsvc-skysilk/Half-sugar-s-gift/releases/download/S-1.0.1-2/Half.sugar.s.gift.S-1.0.1-1.zip)|
|1.0.0|17.3.0+|[下载](https://github.com/hvtXsvc-skysilk/Half-sugar-s-gift/releases/download/v1.0.0/Half.sugar.s.gift.1.0.0.zip)|

# 预发布版本

|HSG版本|对应Among Us版本|下载链接|
|---------|---------|---------|
|S-1.0.1-2|17.3.0+|[下载](https://github.com/hvtXsvc-skysilk/Half-sugar-s-gift/releases/download/S-1.0.1-2/Half.sugar.s.gift.S-1.0.1-2.zip)|
|S-1.0.1-1|17.3.0+|[下载](https://github.com/hvtXsvc-skysilk/Half-sugar-s-gift/releases/download/S-1.0.1-2/Half.sugar.s.gift.S-1.0.1.zip)|

# 加入我们的群聊：  
  <a href="https://qun.qq.com/universal-share/share?ac=1&authKey=Xns8hwheNCb6PUuL%2BHKNid3VktgaQsMQ7QA%2Bt1mDAqjYGd0B%2FbrMDlqG4cZl3XEw&busi_data=eyJncm91cENvZGUiOiIzNjI0NzQ5NDUiLCJ0b2tlbiI6IlU5SUNsbXh4b2syQTJ5K1VIUXlTVWNCZVRWYXhMRjVWR0UxVkliRUF3UnRUS2NGaGxRUmQxTFhUWXgvVUMvWDgiLCJ1aW4iOiIxOTA4OTEzOTA0In0%3D&data=AjSzwAs1LMCESZnMxBTzBix6Yj-ZjcW-h_LaXyHlwd2TSoJpALBxKZkLSSuhuyI3UG8pFkqdo3Ozj0MXf739GfOIOt1D0_5xK6F_iuYieKU&svctype=5&tempid=h5_group_info">
    <img src="https://img.shields.io/badge/QQ-12B7F5?&logo=qq&logoColor=white&style=for-the-badge" alt="QQ" />
  </a>    

# 说明
- 本项目为[**Nebula on the Ship**](https://github.com/Dolly1016/Nebula)的职业插件，新增了一些职业以及指令。
> [!NOTE]
> 本项目的部分职业为AI创作。具体请查看
[来源文档](ATTRIBUTIONS.md)  

# 导入方法
- 1.下载[Nebula on the Ship](https://github.com/Dolly1016/Nebula)模组并且正确导入，正确运行一次。  
- 2.在游戏目录下应新建的**Addons**目录加入本项目的压缩包文件。  

- 3.导入完毕。
- **⚠️注意:不要解压本压缩包文件**

# 常见错误解决方式
这将会教你一些常见的无法进入游戏/插件未生效的问题解决方案。  
一. 加载阶段时，显示一个骷髅头，中间有一段类似于“系统找不到指定的文件'winget'”或者包含winget的类似字样：  
Q&A：  
Q： 是什么导致的这个问题？  
A： 因为加载NoS需要.NET 8 SDK，如果电脑上没有，那么NoS会自动调用winget（一个下载工具）来下载.NET 8 SDK。如果你的电脑没有安装Winget，就会导致本错误。  
解决方案：  
- 下载 [.NET8 SDK（Windows X64）](https://dotnet.microsoft.com/zh-cn/download/dotnet/thank-you/sdk-8.0.423-windows-x64-installer)  
- （备选方案）或下载 [Winget（Microsoft store链接）](https://www.microsoft.com/p/app-installer/9nblggh4nns1)    



二. 进入游戏未加载本插件。    

首先需要进行问题排查。 如果你别的职业/功能插件也没被加载，那么，去下载[.NET8 SDK](https://dotnet.microsoft.com/zh-cn/download/dotnet/thank-you/sdk-8.0.423-windows-x64-installer)。如果只是没有HSG插件，
打开NoS根目录\Cache\Dll\包含“Half Sugar's Gift”的文档文件（注意，是.log的文档文件） 搜索有没有ERROR字段，如果有，将本日志，你的运行环境（比如电脑系统版本）原封不动的发送到hvtXsvc@Outlook.com。  
如果没有，重新尝试导入。  
# 感谢以下贡献者
半糖——策划  
[hvtXsvc](https://github.com/hvtXsvc) —— 主开发者  
[猫猫](https://github.com/AfishMW) —— 开发者  
[Nesplayer](https://github.com/Nesplayer) —— wiki开发  
曦曦——主画师  

# 如何为本插件贡献？
请查看[贡献文档](CONTRIBUTING.md)！

# 📅 更新日志

---

### 🎯 版本 1.0.1.4 Dev20
> **更新内容：** 添加部分代码和 v1014 的资源

---
# 免責聲明
本模组与Among Us或Innersloth LLC均无关联，其内容亦未获得Innersloth LLC的认可或赞助。本模组包含的部分素材归Innersloth LLC所有© Innersloth LLC
