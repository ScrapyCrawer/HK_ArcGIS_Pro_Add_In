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

- ✅ Windows 10/11 (64-bit)
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
graph LR A[准备数据] --> B[选择研究区域] B --> C[配置 POI 数据] C --> D[设置权重] D --> E[运行分析] E --> F[查看结果] F --> G[导出评分地图]

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

- **评分图层**：自动添加到地图，使用色带渲染（红→黄→绿）
- **属性表**：包含各 POI 评分字段和综合 Rating 字段
- **统计报告**：显示分析区域数量、平均评分等

---

## 📂 项目结构
HK_AREA_SEARCH/ │ ├── 📁 Business/                           # 🎯 业务逻辑层 │   │ │   ├── 📁 Distance/                       # 📏 距离分析模块 │   │   ├── DistanceService.cs            # 距离计算服务（主控制器） │   │   │                                  # - ExecuteAsync(): 批量 POI 距离计算 │   │   │                                  # - GenerateUniqueFieldName(): 字段名生成 │   │   │                                  # - CreateWorkingCopy(): 工作副本创建 │   │   │ │   │   ├── EuclideanDistanceCalculator.cs # 欧氏距离计算器 │   │   │                                  # - CalculateAsync(): 矢量→栅格距离 │   │   │                                  # - SetMaxDistance(): 距离阈值配置 │   │   │ │   │   ├── ZonalStatisticsService.cs     # 分区统计服务 │   │   │                                  # - ExecuteZonalStatsAsync(): 区域统计 │   │   │                                  # - JoinFieldAsync(): 字段连接 │   │   │ │   │   └── RasterReclassifier.cs         # 栅格重分类器 │   │                                      # - ReclassifyAsync(): 等间隔/自定义分级 │   │ │   ├── 📁 Divide/                         # ✂️ 分区分析模块 │   │   ├── DivideService.cs              # 分区服务（主控制器） │   │   │                                  # - ExecuteAsync(): 分区工作流 │   │   │                                  # - DivideByDistrict(): 按行政区分割 │   │   │                                  # - DivideByArea(): 按面积分割 │   │   │ │   │   ├── AreaFilter.cs                 # 面积过滤器 │   │   │                                  # - FilterByAreaAsync(): 面积范围筛选 │   │   │                                  # - EnsureAreaFieldAsync(): Shape_Area 字段管理 │   │   │                                  # - BuildWhereClause(): SQL 条件构建 │   │   │ │   │   ├── GeometryProcessor.cs          # 几何处理器 │   │   │                                  # - SimplifyGeometry(): 几何简化 │   │   │                                  # - RepairGeometry(): 几何修复 │   │   │                                  # - ValidateTopology(): 拓扑验证 │   │   │ │   │   └── ConstraintMerger.cs           # 约束合并器 │   │                                      # - MergeConstraints(): 多约束叠加 │   │                                      # - EraseConstraints(): 擦除不适宜区域 │   │ │   └── 📁 Rating/                         # ⭐ 评分模块 │       ├── RatingService.cs              # 评分服务（主控制器） │       │                                  # - CalculateComprehensiveRating(): 综合评分 │       │                                  # - ApplyWeights(): 权重应用 │       │                                  # - NormalizeScores(): 归一化处理 │       │ │       └── IntersectionAnalyzer.cs       # 交集分析器 │                                          # - AnalyzeIntersection(): 图层相交分析 │                                          # - ExtractOverlapAreas(): 重叠区域提取 │ ├── 📁 Infrastructure/                     # 🛠️ 基础设施层 │   │ │   ├── 📁 Services/                       # 🔧 基础服务 │   │   ├── TempFileManager.cs            # 临时文件管理器 │   │   │                                  # - CreateTempFile(): 创建临时文件 │   │   │                                  # - RegisterTempFile(): 注册文件跟踪 │   │   │                                  # - CleanupAll(): 批量清理 │   │   │                                  # - GetTempFolder(): 临时目录管理 │   │   │ │   │   └── SymbologyManager.cs           # 符号系统管理器 │   │                                      # - ApplyGraduatedColors(): 色带渲染 │   │                                      # - CreateClassBreaks(): 分级断点 │   │ │   └── 📁 Common/                         # 📦 公共组件 │       └── Constants.cs                  # 常量定义 │                                          # - RATING_FIELD: "Rating" │                                          # - AREA_FIELD: "AREA_M2" │                                          # - NUM_CLASSES: 10 │ ├── 📁 Models/                             # 📊 数据模型层 │   ├── POIDataItem.cs                    # POI 数据项模型 │   │                                      # 属性: │   │                                      # - DataPath: 数据路径 │   │                                      # - Distance: 影响距离 │   │                                      # - Weight: 权重系数 │   │                                      # - CustomInterval: 自定义间隔开关 │   │                                      # - IsRasterData: 栅格数据标志 │   │ │   └── IntervalClassItem.cs             # 区间分类项模型 │                                          # 属性: │                                          # - StartValue: 起始值 │                                          # - EndValue: 结束值 │                                          # - ClassValue: 分类值 │ ├── 📁 ViewModels/                         # 🎨 视图模型层 (MVVM) │   └── main_dockpaneViewModel.cs         # 主面板 ViewModel │                                          # 命令: │                                          # - StartAnalysisCommand: 开始分析 │                                          # - ClearPOIDataCommand: 清除数据 │                                          # - OpenCustomIntervalDialogCommand │                                          # 属性: │                                          # - POIDataList: POI 数据集合 │                                          # - SelectedAnalysisArea: 选中研究区域 │                                          # - MinArea/MaxArea: 面积过滤范围 │ ├── 📁 Views/                              # 🖼️ 视图层 (WPF) │   └── main_dockpane.xaml                # 主面板界面 │                                          # UI 组件: │                                          # - MapLayerComboBox: 图层选择 │                                          # - POIDataGrid: POI 数据表格 │                                          # - AreaFilterControls: 面积过滤控件 │                                          # - ProgressBar: 进度条 │ ├── 📁 Images/                             # 🎨 资源文件 │   └── [图标文件]                         # 工具栏图标、按钮图标 │ ├── Config.daml                           # ⚙️ ArcGIS Pro 配置文件 │                                          # - 定义 DockPane、Button、Tool │                                          # - 配置 Ribbon 界面 │                                          # - 注册插件组件 │ ├── Module1.cs                            # 🔌 插件模块入口 │                                          # - OnClick(): 插件初始化 │                                          # - CanUnload(): 卸载检查 │ ├── HK_AREA_SEARCH.csproj                 # 📦 项目文件 │                                          # - 依赖包配置 │                                          # - 编译选项 │ └── README.md                             # 📖 项目文档

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
