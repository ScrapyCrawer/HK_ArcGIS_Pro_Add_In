# 🏗️ HK Area Search - 香港适宜建设土地选址综合分析系统

[![ArcGIS Pro](https://img.shields.io/badge/ArcGIS%20Pro-3.0+-blue.svg)](https://www.esri.com/en-us/arcgis/products/arcgis-pro)
[![.NET](https://img.shields.io/badge/.NET-6.0-purple.svg)](https://dotnet.microsoft.com/)
[![C#](https://img.shields.io/badge/C%23-10.0-green.svg)](https://docs.microsoft.com/en-us/dotnet/csharp/)

> 基于 ArcGIS Pro SDK 开发的智能化土地选址分析插件，通过多因子综合评价模型，为城市规划和土地开发提供科学决策支持。

## 📋 目录

- [功能特性](#-功能特性)
- [技术栈](#-技术栈)
- [快速开始](#-快速开始)
- [使用指南](#-使用指南)
- [项目结构](#-项目结构)
- [开发说明](#-开发说明)

---

## ✨ 功能特性

### 🎯 核心功能

- **🗺️ 智能分区分析**
  - 支持按行政区划、地块面积自动分割研究区域
  - 可自定义最小/最大面积阈值过滤
  - 支持复杂几何图形处理（合并、裁剪、简化）

- **📍 POI 距离分析**
  - 支持多类型 POI（医院、学校、交通等）同时分析
  - 自动计算欧氏距离并生成评分字段
  - 支持正向设施（距离越近越好）和负向设施（距离越远越好）
  - 自定义距离阈值和评分区间

- **🎨 多因子综合评价**
  - 支持自定义权重配置
  - 自动归一化处理
  - 等间隔分级或自定义分级
  - 可视化评分结果
    
- **📊 约束条件叠加**
  - 支持多图层约束条件（水体、坡度、保护区等）
  - 灵活的叠加分析策略
  - 自动生成适宜性评分

- **💾 数据管理**
  - 智能临时文件管理，避免污染原始数据
  - 自动工作副本创建
  - 批量清理中间文件

### 🚀 高级特性

- ✅ **异步处理**：全流程异步操作，UI 响应流畅
- ✅ **错误恢复**：详细的错误日志和异常处理
- ✅ **格式兼容**：支持 Shapefile、GeoJSON、KML 等多种格式
- ✅ **中文支持**：完整的中文界面和字段别名
- ✅ **批量处理**：支持多个 POI 数据批量分析

---

## 🛠️ 技术栈

### 开发环境

| 技术 | 版本 | 说明 |
|------|------|------|
| **ArcGIS Pro SDK** | 3.0+ | 地理空间分析核心 |
| **.NET** | 6.0 | 运行时框架 |
| **C#** | 10.0 | 开发语言 |
| **WPF** | - | 用户界面框架 |
| **Visual Studio** | 2022+ | 开发工具 |

### 核心依赖
<PackageReference Include="ArcGIS.Desktop.Framework" Version="3.0+" /> <PackageReference Include="ArcGIS.Desktop.Core" Version="3.0+" /> <PackageReference Include="ArcGIS.Desktop.Mapping" Version="3.0+" /> <PackageReference Include="ArcGIS.Core.Data" Version="3.0+" />

---

## 🚀 快速开始

### 前置条件

- ✅ Windows 11 (64-bit)
- ✅ [ArcGIS Pro 3.0+](https://www.esri.com/en-us/arcgis/products/arcgis-pro) (已授权)
- ✅ [.NET 6.0 SDK](https://dotnet.microsoft.com/download/dotnet/6.0)
- ✅ [Visual Studio 2022](https://visualstudio.microsoft.com/) (可选，用于开发)

### 安装步骤

#### 方式一：直接安装（推荐）

1. 从 [Releases](https://github.com/ScrapyCrawer/HK_ArcGIS_Pro_Add_In/releases) 下载最新版本的 `.esriAddinX` 文件

2. 双击安装文件，ArcGIS Pro 会自动安装插件

3. 重启 ArcGIS Pro，在 **Add-In** 选项卡中找到 **HK Area Search** 工具

#### 方式二：从源码构建
克隆仓库
git clone https://github.com/ScrapyCrawer/HK_ArcGIS_Pro_Add_In.git cd HK_ArcGIS_Pro_Add_In
切换到主分支
git checkout main
使用 Visual Studio 打开解决方案
HK_AREA_SEARCH.sln
按 F5 编译并调试

---

## 📖 使用指南

### 基础工作流程
A[准备数据] --> B[选择研究区域] B --> C[配置 POI 数据] C --> D[设置权重] D --> E[运行分析] E --> F[查看结果] F --> G[浏览评分地图]

### 详细步骤

#### 1️⃣ 准备数据
研究区域图层：
•	格式：Shapefile (.shp)、GeoJSON、KML
•	几何类型：面 (Polygon)
•	坐标系：建议使用投影坐标系（如 HK 1980 Grid System）
POI 数据：
•	格式：Shapefile (点/面)、raster
•	必要字段：几何信息

#### 2️⃣ 启动分析

1. 在 ArcGIS Pro 中打开地图
2. 点击 **Add-In** 选项卡 → **HK Area Search**
3. 在面板中配置参数：
研究区域: [选择面图层] 分割方式: 按行政区 / 按面积 最小面积: 100 平方米 (可选) 最大面积: 10000 平方米 (可选)
POI 配置，例如:
•	医院: [hospital.shp], 距离=1000m, 权重=0.3
•	学校: [school.shp], 距离=800m, 权重=0.2
•	地铁: [metro.shp], 距离=500m, 权重=0.3
•	垃圾站: [waste.shp], 距离=-500m, 权重=0.2 (负向)

#### 3️⃣ 查看结果

- **评分图层**：自动添加到地图，可手动渲染
- **属性表**：包含各 POI 评分字段和综合 Rating 字段
- **统计报告**：显示分析地块属性、评分等

---

## 📂 项目结构

```
HK_AREA_SEARCH/
├── Business/                          # 业务逻辑层
│   ├── Distance/                      # 距离分析模块
│   │   ├── DistanceService.cs
│   │   ├── EuclideanDistanceCalculator.cs
│   │   ├── ZonalStatisticsService.cs
│   │   └── RasterReclassifier.cs
│   ├── Divide/                        # 分区分析模块
│   │   ├── DivideService.cs
│   │   ├── AreaFilter.cs
│   │   ├── GeometryProcessor.cs
│   │   └── ConstraintMerger.cs
│   └── Rating/                        # 评分模块
│       ├── RatingService.cs
│       └── IntersectionAnalyzer.cs
├── Infrastructure/                    # 基础设施层
│   ├── Services/
│   │   ├── TempFileManager.cs
│   │   └── SymbologyManager.cs
│   └── Common/
│       └── Constants.cs
├── Models/                            # 数据模型
│   ├── POIDataItem.cs
│   └── IntervalClassItem.cs
├── ViewModels/                        # 视图模型
│   └── main_dockpaneViewModel.cs
├── Views/                             # 视图
│   └── main_dockpane.xaml
└── Config.daml                        # ArcGIS Pro 配置
```

2. **配置调试**

// 1. 依赖注入 public class DistanceService : IDistanceService { private readonly TempFileManager _tempFileManager;
public DistanceService(TempFileManager tempFileManager)
{
    _tempFileManager = tempFileManager;
}
}
// 2. 异步操作 public async Task<string> FilterByAreaAsync(string inputPath, double? minArea, double? maxArea) { return await QueuedTask.Run(async () => { // ArcGIS 操作代码 }); }
// 3. 工作副本模式（避免污染原始数据） string workingCopy = await CreateWorkingCopy(originalPath); // 在工作副本上进行操作

### 构建与发布
bash
1. 清理项目
dotnet clean
2. 恢复依赖
dotnet restore
3. 构建项目（Release 模式）
dotnet build -c Release
4. 生成 .esriAddinX 文件
输出位置：bin\Release\HK_AREA_SEARCH.esriAddinX

---

## 🤝 贡献指南

欢迎任何形式的贡献！

### 如何贡献

1. **Fork** 本仓库
2. 创建特性分支 (`git checkout -b feature/AmazingFeature`)
3. 提交更改 (`git commit -m 'Add some AmazingFeature'`)
4. 推送到分支 (`git push origin feature/AmazingFeature`)
5. 提交 **Pull Request**

### 开发分支说明

- `main` - 稳定版本，用于发布
- `Refactor` - 重构开发分支
- `feature/*` - 功能开发分支
- `bugfix/*` - 错误修复分支

### 代码审查标准

- ✅ 遵循 C# 命名规范
- ✅ 添加 XML 注释文档
- ✅ 单元测试覆盖率 > 60%
- ✅ 无编译警告
- ✅ 通过代码格式化检查

---

## 📧 联系方式

- **GitHub Issues**: [提交问题](https://github.com/ScrapyCrawer/HK_ArcGIS_Pro_Add_In/issues)

---

<div align="center">

Made with ❤️ by [ScrapyCrawer](https://github.com/ScrapyCrawer)

</div>
