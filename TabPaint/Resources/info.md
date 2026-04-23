Tab Paint 0.1

initial commit

TabPaint 0.2

TabPaint 0.3

TabPaint 0.4

基本实现功能但一堆bug

TabPaint 0.5

修一堆bug

Sodium Paint 0.5.1
3.colorpick改鼠标指针
4.代码拆分/代码整理
1.大图(>4k)先加载480p缩略图
17.预览图大小(图片显示不全)
25.多加几个自带颜色按钮
1.改名为TabPaint，修改图标

TabPaint 0.6
1.selectpreview拖之前部分(缩放部分?)不显示bug
2.按钮上悬浮弹出提示文字
3.itool非select时支持粘贴
4粘贴的图片大小>画布时自动调整画布
5粘贴撤销
6裁剪selection无法redo
7裁剪selection回撤后相关区域消失
8.全图选中无法提交
9.选中工具紫色边框(暂时)
10.保存刷新预览图
11.select框宽度随放大缩小不改变
12.高亮选中颜色(样式)
13.pensizebar调整样式
14.最后一张/第一张时info提示
15.(右键设置界面)
16.chop按钮随着状态改变enable/disable
17.拖动/复制文件变成画板selection

Tab Paint 0.61
1.画布边缘8xhandle拖拽调整大小
1.1缩略图显示handle
1.2边框虚线
1.3handle不随缩放调整大小
1.4拖动生成文件损坏
1.5复制selection无效bug
11.超大笔刷撤销残留
9.加入荧光笔(仅黄色)
10.马赛克画笔(高斯模糊)
12.其他按钮(保存等)悬浮文字
13.铅笔隐藏resizebar
7.1text控件边框难以拖动
7.2text控件commit未commit连续激活
8.text控件toolbar美化
7.text控件无法写入第二行bug
5.右键菜单闪烁bug
3.点击拖动条不要commit选择框

TabPaint 0.6.2
1.imagebar右侧出现按钮（+）
2.imagebar蓝色双选Bug
3.启动时ctrl+n新图片不附加在末尾
4.左右转按钮专门拿出来，和裁剪放在一起
5.+按钮美化
6.左侧+按钮
7.看图栏左侧3x按钮(清空未编辑图片，保存所有图片，放弃所有编辑),仅按钮
8.ctrl+n在imagebar这张图右侧创建新图
9.缩略图左上角用红点区分是否保存(未编辑无图标，编辑未保存红色，已经保存无图标?)
10.缓存目录备份未保存图片(停笔3s后保存，后台进行,切图和关闭程序时立刻保存)
11.重新启动时加载未保存图片
12.实装左侧3按钮功能
13.首次启动在用户\appdata\local\tabpaint下维护session.json上次打开的文件
14.点击imagebar双重加载bug
15.未命名图片编号

TabPaint 0.6.3
1.未修改未命名不应为蓝点
2.imagebar点击卡顿
3.title文件名显示为缓存bug
4.重复点击相同图片tab不要重新加载
5.imagebar拖拽图片增加文件
6.ctrl+拖拽imagebar中的图片生成文件?
7.拖出imagebar新图片缩略图bug
8.新图片ctrl+s保存弹窗功能
9.imagebar缩略图同步(停笔3s后同步，切换图片时立刻同步，时间随图片大小动态调整)
10.启动时红点消失bug
11.左侧+默认显示位置bug
12.清空列表后当前画板改为新建的"未命名"(而不是留存)，thumbnail也同步
13.未命名编号系统bug(点击左右+生产的新建图片全是未命名0)
14.discardall后删除缓存/所有新建图片/已有图片的更改，彻底回到初始状态
15.鼠标拖拽imagebar？
16.slider残留阴影

TabPaint 0.6.4
1.imagebar选中图片无法居中bug
2.未命名图片创建在现有图片右侧
3.中键关闭标签页
4.通过关闭所有未编辑图片关掉全部后生成未命名0
5.全新未命名重启时不应显示(已修复?)
6.关闭tab后切换到上(下)一个tab,没有则生成空图片
7.放弃更改立刻刷新画面(如何触发?已修复？)
8.a文件夹下的未保存图片在b文件夹下启动时不会加载
9.新建图片画布位置bug
10.新图保存默认位置为打开的文件夹
11.保存未命名图片默认不应为未命名而是未命名-x

TabPaint 0.6.5

1.点击新imagetag时一定概率框选跳回原tag一瞬间(如何触发?已经好了?)

2.imagebar图片加载不全bug（再次出现）

3.title图片数量及时更新(保存，第二次切换未命名图片)

4.closetab和新加载冲突bug（关不掉页面）

5.删除图片时title图片数量和编号及时更新()

6.切换图片备份的是缩略图而不是上一张原图(如何触发?已经好了?)

7.title*标识应随dirty及时更新
8.拖出imagebar误触拖入bug

9.←→键切图imagebar蓝色边框消失

10.右键imagebar支持剪切复制粘贴删除

11.大图连续切换程序崩溃bug

12.抬笔保存过(且无修改)切换图不应再保存

13.discardall彻底删除整个缓存文件夹

14.大图片文件夹titlebar编号
TabPaint 0.7

2.Space + 拖动 = 移动画布,
3.shift+滚轮左右移动画布

4.ShapeTool圆，矩形

5.alphablend-bug

6.ShapeTool显示位置

7.添加直线，圆角矩形，箭头

8.ShapeTool图标切换及紫色强调

9.selecttool切回

10.预览图加载偶尔多出一个

11.鼠标悬浮在底部栏某个图标(位于适应窗口大小左侧)上显示图片大小，dpi,exif等一堆信息

12.xy|size|select数值之间加竖线分隔

13.透明灰白格子

14.拖入toolbar/menu切换工作区

15.拖入图片提示(灰色遮罩，imagebar位置显示此处增加新图片，画布位置显示此处插入画板，toolbar/menu提示切换文件夹)

16.非96px图片Selection控件bug

17.<480p图thumbnail加载不了bug

18.预览图末尾一定概率加载不全(少10个)

TabPaint 0.7.1

1.ctrl+w关闭标签页

2.滤镜窗口美化

3.监听剪切板开关(bottombar图片详细信息左侧

4.(16k+图)被压缩警告

5.ctrl+shift+v粘贴为新标签页

6.工作区最外圈不要显示虚线框(很少复现?)

7.imagebar-thumbnail右键菜单添加打开所在文件夹

8.(menubar最右端)简单的设置关于页面(左侧分页，暂时只有设置和关于两页，左下角显示版本，右下角确定)设置页面放三个选项:暂时放三个选项:画布上滚轮放大缩小/滚动切图/上下移动画布(仅界面)

9.放大后边缘留空方便拖拽handle

10.delete删除文件及相关bug

11.多文件拖入canvas全部成新标签页

12.非96dpitextbox打印位置bug

13.窗口拖拽改变大小失效bug

14.窗口最小大小

Tab Paint 0.7.2

1.imagebar-slider末端比例bug

2.imagebar-slider滚轮滚动

3.图片没铺满屏幕时应该隐藏slider

4.新建图片增加**`_imageFiles`和count,名字为一个特殊标识符(如图片编号)；每次新建图片更新整个imagefiles，保存时改path**

5.拖拽排序后序号update

6.创建未命名1，2→关闭未命名1→生成未命名3bug

7.编辑的未命名重启加载名字全部变成未命名0bug

8.临时保存的未命名无法在切回时读取bug

9.重启加载未命名导致数量统计错误bug

10.点击关闭按钮后立刻隐藏窗口

11.selection控件过多触发

12.右键菜单固定缩放大小

13.拖入图片→外部pointerup后遮罩不消失及闪烁

14.已保存tab无法加载(再次出现)bug

Tab Paint 0.7.3

1.mainwindow.xaml拆成几个UserControl

2.停笔3s保存改小一点

3.缩略图绘图崩溃bug

4.ExtractRegionFromSnapshot出界bug（画笔快速在大图上绘制可复现）(已经缓解?)

5.f11最大化/还原

6.自定义颜色编辑界面

7.刷子，图形，旋转翻转悬浮淡紫色边框

8.zoom最小值自适应于图片大小

9.颜色间距略微调大一点，非悬浮时外面包白边

10.画笔算法优化一阶段(100-600px直径性能，低px毛刺)

11.设置系统(class)

12.实装设置系统(默认笔刷大小，固定缩放倍数，监听剪切板，上次启动时工具)

13.exif信息不准确bug

14.调整大小窗口改成fluentui

15.剪切板监视双击bug

16.MyStatusBar.ZoomSliderControl.ValueChanged间隔过大bug

17.右键imagebar粘贴修复

18.icon模糊与settings版本同步bug

Tab Paint 0.7.4

1. 画布上右键不应落下画笔
2. 优化鼠标悬浮按钮上文字功能(一阶段)
3. 统一设计风格为fluent UI(第一阶段，优先在https://bennymeg.github.io/ngx-fluent-ui/找svg,确保粗细一致)
4. ←→未updatetitlebar
5. 旋转图片出现透明图层bug
6. 多文件粘贴canvas全部成新标签页
7. 监听剪切板绑定快捷键
8. 笔刷粗细slider非线性调节
9. 左右转图片八角handle重新布局
10. 画笔大小示意图不要超出canvas
11. 方形笔大小示意图改成方形
12. 灰白格子不应随缩放改变大小
13. 切图selection应当清空
14. 相同图片文件imagebar粘贴后应当switchto
15. text控件支持（左/中/右对齐，删除线，背景填充?,字体选择框fluentui化）
16. bottombar显示文件体积
17. 文本编辑条字体的后台加载
18. bce/tts窗口不触发dirtybug
19. tts和bse增加输入数字和重置
20. selectioncommit撤回失效bug
21. selection需两步撤销bug
22. 切画笔时selectionpreview不消失
23. shape无法拖拽改变大小bug
24. shape双重撤销bug
25. shape显示笔刷粗细slider
26. 旋转后commit之前的selection内存bug(似乎好了)
27. 放大至1000%左右画布异常移动bug(其实是特性)

T

Tab Paint0.7.5

1.常见界面美化(最大放大尺寸设为50x,colorwheel改成svg,(参考画图)titlebar和menubar一个颜色，menubar-toolbar之间画线)

2.tts/bse窗口确定按钮高度调整，双击滑块复位

3.statusbar/slider和倍数选择美化，间距略微调大

4.画笔slider自动改变高度

5.画笔增加不透明度bar(仅界面和部分支持)

6.颜色编辑界面第二阶段(白色风格，rgb/hsv调整，启用自定义颜色（点击+存到里面，不支持存档），多增加两排”基本颜色”和自定义颜色，基本颜色增加tooltip,基本颜色应当覆盖各个色系，hue换成圆角矩形且判断范围更大)

7.mica概率性初始化失败,mica随窗口焦点enable/disable效果(chromewindow实现)\

1. menubar支持拖动窗口
2. texttool生成半透明部分bug
3. textbox打印和预览字体大小不一
4. 取色光标加载异常(多次切换后出现)
5. zoomslider非线性滑动
6. 颜色选择按钮失效(源自于usercontrol拆分)
7. 橡皮用第二颜色擦除；texttool背景填充用第二颜色
8. crop后产生透明区域(需要更新)

Tab Paint0.7.6

1. 裁剪后crop按钮未及时disable
2. 全屏→拖拽title无法恢复大小bug
3. titlebar不支持双击全屏/恢复bug（暂时menu不支持）
4. 直接打开程序时不再加载debug文件夹(根据debug/release模式区分)
5. opacitybar拖拽时显示透明度
6. selectionpreview拖拽缩小(仅缩小)后->拖拽preview→preview双倍缩小
7. selection拖拽handle改变大小时就应当删除原画布
8. 旋转绑定ctrl+l/r快捷键
9. 选中preview时旋转功能为旋转preview
10. 在未命名图片上画图时保存在上一张非未命名图上(似乎好了)
11. 创建未命名→不绘画→切回原图失败bug
12. 旋转裁剪后更新thumbnail
13. onclosing保存和timer保存冲突bug
14. 已经加载thumbnail并瞬间显示的图片不应在之后步骤卡住ui线程，阻塞→切图(已经好了?)
15. selecttool拖到外面显示程序缓冲区不足崩溃bug
16. 拉动/旋转/填入大图片/整图undo/crop等操作及时改变statusbar画布大小显示
17. 画布调整大小按钮dirty状态和updateui修复
18. slider滚轮头尾失效bug(因此20图的imagebar上完全不能用)
19. 图片不满时不隐藏imageslider而是变成灰色和低透明度
20. ctrl拖拽生成图片改成直接拖拽；
21. 拖拽文件出错bug(拖拽成了经过且邻接的tab，双重触发事件)
22. 空filepath打开时退出错误及不能绘画，scanfolder错误
23. 未命名图片加载不能进入virtual分支?（同时文件大小错误）
24. 统一createnewtab方法的使用
25. discardall清空selection和拖入图片和virtualtab
26. 关闭所有未编辑图片在无dirty图片时能全部关闭，一旦有dirty则无效(和加载冲突?)
27. 关闭所有未编辑图片不包括未加载部分bug
28. 关闭所有未编辑图片未命名计数错误(应该好了)
29. 运行时删除缓存文件崩溃（程序关闭时）
30. 强制结束进程即使保存了也无法恢复图片
31. 优化imagebar拖拽手感(线条动画，thumbnail间可响应事件)
32. 空filepath打开时创建新图和切换新图错误(似乎好了)
33. filePath错误文件不存在会引发Undo null异常(调试时易产生)
34. filepath支持填入文件夹（似乎没必要）
35. 点击打开文件不再切换工作区；支持多个文件
36. 不粘贴自己剪切/复制的tab
37. 缩放倍数小时开启模糊显示canvas
38. 缩略图非点对点显示?
39. 外部commit-选择框时-lag错误
40. selecttool剪切功能失效
41. 拉动画布后增加一个方法确保上/下/左/右边缘仍然暴露
42. 设置isfixedzoom=true重启后放大倍数combobox数字消失bug
43. 文件夹path不支持discardall
44. 画笔轻重绘图修复（圆形、方形、喷枪、水彩）

Tab Paint 0.8

1.看图画图模式分离(tab切换)

2.toolbar,bottombar，menu隐藏

3.(模式切换缩放倍率和窗口位置不变)

4.canvas大小拖拽handle隐藏

5.imagebar-dirty标识隐藏，左右侧新建隐藏，imagebar左侧三个按钮隐藏

6.左键拖拽图片,图片无法拖拽时拖拽窗口

7.双击全屏

8.ctrl+l/r键旋转图片

9.(画图转看图)当前工具为selecttool/shapetool/texttool时cleanup

10.看图模式ctrl+l旋转/加载图片大规模出现灰白底(return位置错误)

11.titlebar-(最小化)左侧提供一个按钮画图看图切换

12.tab节流防止闪烁

13.自定义scrollerviewer样式，以及拖拽判定

14.texttool下单击出现选框

15.zoom放大缩小时窗口上灰色半透明提示

16.优化启动速度(~200ms)

17.分步加载控件(ContentControl )

18.svg改用`StreamGeometry`

19.放大倍数非常大时select的preview出现轻微的拉伸现象

TabPaint 0.8.1

1. loading时放大崩溃
2. 窗口改windows缩放后会模糊(改**`app.manifest`）(可能产生的bug尚未完全测试)**
3. 设置/编辑颜色界面按esc退出
4. 16x下selectionpreview错位(似乎好了)
5. select左上五点改变大小时能拖出preview范围bug
6. Texttool,canvasresizer同理(左上五点改变大小,超出画布范围)
7. 修复undoredobutton状态更新
8. text工具应用笔刷轻重设置
9. 单opacitybar布局更新,shapetool隐藏笔刷轻重slider,相关代码整理
10. 铅笔应用笔刷轻重修复
11. selection向外拖拽不应清除选区
12. selection向外拖拽返回时触发遮罩
13. 存在未commit选区时撤销应giveup
14. 拖入大图后select粘连bug
15. 拖入selection的非96pxpreview错误(粘贴同理)
16. 拖入selection后清空拖入区域的背景bug
17. preview拖拽概率性卡顿(似乎和遮罩触发有关,应该好了)
18. 程序启动第一次使用shapetool并拖拽preview会导致背景变白（似乎好了）
19. shapetool矩形commit后实为圆角矩形bug
20. shapetoolPointerup时重置画布大小bug
21. 旋转大小改变过的preview预览图变形bug
22. 图片resize后保存功能失效
23. 减少messagebox的使用，改用showtoast
24. 加入onsavealldoubleclick保存新建文件
25. 点击保存所有图片后“已保存x张图片”数字错误（无论是否保存都计入）
26. 极快速拖拽imagebar-thumbnail在canvas上的遮罩leave不消失bug
27. canvas遮罩显隐动画
28. 高dpi屏幕canvas遮罩显隐闪烁bug
29. 拖拽imagebar-thumbnail应用图片最新副本
30. 支持ico,gif(单帧)，".heic",".tif”查看
31. 一键抠图(按需下载，**ONNX Runtime，Hugging Face/ModelScope直链下载，MD5检测**)
32. 右键菜单增加ocr识别(windows自带Windows Media OCR API，一键复制到剪切板)
33. ocr通过selection选区识别
34. 右键菜单添加色差抠图功能
35. 色差抠图支持根据点击位置确定参考颜色
36. 右键菜单屏幕取色器
37. 右键菜单复制颜色代码
38. 右键菜单智能裁切空白
39. 右键菜单折叠为小工具(扳手图标)
40. 只运行一个实例，后续打开图片添加进tab
41. 最近文件列表，清除文件列表的功能
42. 极快(<1ms)的benchmark来估测电脑性能(考虑cpu核心数，频率，分辨率，系统位数，极小的整数运算性能等因素)
43. 4k+图加载进度条(用_imageSize?，估测，加载完缩略图显示30%,之后平缓增大，根据分辨率和性能评分确定速度
44. 快速切图显示之前的进度条，并且最终卡在95%bug

TabPaint 0.8.2

1. 加载进度条数值优化(10%开始，时间调整，4k阈值和性能有关)
2. 屏幕取色器圆点遮挡取色器工作
3. 屏幕取色器不应增加色环；应用至currentcolor
4. shape工具不应允许crop
5. 小工具全部加一套快捷键（ctrl+alt+1~6）
6. 自然语言排序imagebar图片
7. shape未commit时应当可以撤销
8. selection框选未拖拽时应当可以撤销(隐藏preview)
9. shape可能撑大画布
10. 增大箭头shape的尖端
11. 取色器加入放大镜
12. 向外透明jpg图片thumbnail拖拽变成黑白bug（保存同理）
13. 自动色阶
14. 反色
15. 透明图片拖拽selection留下白底bug
16. 右键菜单一键加边框
17. selection拉出选取外→过大弹回→选区大小未更新bug
18. selection在canvas边界来回拖拽preview消失
19. ai一键抠图后必须点击一下canvas才能redo
20. 切换至文件夹menu;imagebar拖到titlebar切换工作区
21. 设置分为(通用/画图设置/看图设置/快捷键设置/高级/关于页面)
22. 快捷键绑定系统-1
23. (高级设置界面)恢复出厂设置
24. 关闭保存会影响第二个程序打开
25. 看图/画图时画布从点对点/双线性插值阈值设置(拖动条)
26. 重采样算法设置（自动，双线性，**Fant ，HighQuality** ）
27. 8k+selection在canvas边界来回快速拖拽偶发卡顿
28. trigger backgroundbackup过多触发bug(pointerdown)
29. 超大图preview防抖策略

TabPaint 0.8.3

1. 标尺
2. 工具提供快捷键ctrl+1-8,shape和pentool默认第一个
3. 看图模式禁止canvas右键
4. 设置-高级界面不能滚动，[关于]放在设置左下角
5. x改成悬浮白色，slider指数化，允许输入数字
6. combobox用fluentui样式，slider样式统一，去掉两行文字
7. 内存管理问题(占用比预想大，切换图越来越大,非内存泄漏而是来不及gc)（初步解决）
8. 自定义颜色持久化(存至sett)
9. selection调节大小应用采样方法设置
10. resize选区时及时更新选区大小显示
11. 更新menu-效果三个图标
12. 颜色编辑界面允许输入alpha值？
13. 颜色编辑slider-thumb外部无法点击bug
14. 透明度绘图(画刷逻辑)
15. scrollwhell/zoominout插值平滑放大缩小
16. 全新画布启动时没有fittowindow
17. 长按→一定时间开始跳过图片，时间越长gap越大
18. 打开图片selection选区未清除Bug
19. ctrl+c复制源文件
20. 支持gif播放（`XamlAnimatedGif` ？或者`GifBitmapDecoder` 手动实现，不支持编辑保存gif，tab切回后看到静态第一帧）
21. statusbar-zoomcombobox点击外部和回车无法应用bug
22. 调节完像素画阈值点确定后用一下setzoom
23. ScreenColorPicker点击后子菜单不能立刻收起bug
24. 不默认加载当前文件夹下所有图片设置(通用)
25. 滚轮放大缩小/切图设置(看图)
26. 启动时为看图模式设置(通用)
27. 默认画图模式启动无法播放gif的bug
28. 看图模式时menubar“文件”菜单移动到tabpaintlogo上
29. **exif功能信息显示(看图模式)**
30. del删除对应图片至回收站，弹出提示框2s,2s内ctrl+z可撤销（画图看图均支持）
31. 选区删除→文件删除防抖
32. 鸟瞰图
33. 正在拖动新建selection时切换到看图模式没有清理掉bug(shape同理)
34. 白底/**透明灰白格子切换（看图模式）**
35. （看图模式）canvas外为深灰色背景**/浅色设置**
36. selection拖拽不支持部分程序bug（概率发生）
37. 一般尺寸画笔跟手程度优化（似乎已解决）

TabPaint 0.8.4

1. autoload=false时打开新文件夹也应当加载同文件夹下的文件
2. 画图模式拖入gif误播放
3. 粘贴进来的图片拖动后在原位留下印记(拖入的没问题)
4. text字体大小框可拖动
5. selectionpreview应用线性插值设置
6. selectionpreview非100%dpi下左右偏移问题(算是修好了)
7. canvas边缘加一圈灰色边框和阴影且大小不变
8. text编辑时ctrl+z删除text框
9. 快速放大canvasresizehandle大小变化不及时
10. “基本颜色”找各色系的颜色
11. selectionpreview在pointerdown拉出窗口边框→窗口外面pointerup→回来→鼠标粘连
12. 自定义颜色/编辑颜色画板/当前颜色加上灰白底
13. 极端小图(1x4),此时resizehandle大小不对
14. 高倍放大下边框渲染出错（>20x）
15. 设置界面美化二阶段(slider，textbox复用mainwindow样式，checkbox写一个fluentui,窗体宽度不足
16. 快捷键框无法点击Bug
17. 生成小画布→放大→撤销→此时缩小比例有限制bug
18. 效果menu,翻转给一套快捷键
19. 0字节空文件情况处理(新建画布)
20. 480p缩略图功能没有启用bug
21. 调整大小改版为”缩放画布”和裁剪画布，添加slider和输入
22. 调整大小/canvasresize工具可拉出16384px+图bug
23. ctrl+a全选功能拖拽后坐标计算错误,无法拖动
24. ctrl+a产生的选区必然和resizehandle重合
25. 外部粘贴进未commit的selection→ctrl+z→错误的双重撤销Bug
26. 粘贴进来的图片将画布撑大后留下透明底(需更新画布)
27. selection工具删除后为白底/透明底/不改变alpha设置
28. 快捷键切换至取色工具后lasttool=null崩溃Bug
29. 编辑8k大图→切图→切回原图→usedbyanother process，显示错误，再次切换后正常
30. dirty时切图太慢，应该有防文件死锁的async切图机制?
31. 第二次commitselection快速切图时未保存
32. 拖拽选区进入imagebar，显示1s动画后切换到对应页面(并携带这个选区)
33. 仅一张图时隐藏imagebar;menuredo右侧增加+按钮
34. imagebar触控版左右双指滑动

TabPaint 0.8.5

1. textbox中按ctrl+a触发选区全选bug（左右同理）
2. smoothzoomselect边框未及时更新
3. 笔像素直径→半径
4. 看图模式不显示画框
5. textbox连续触发bug(再次出现?)
6. texttool启用时同样隐藏resizehandle
7. 拖拽跳转功能如果undostack只有一个操作，redo无操作则等待事件减半
8. 拖拽选区时使用任何效果工具，选区应自动提交
9. 拉出选区后切换工具statusbar选区值未清空；shapecommit后statusbar选区值未清空
10. 路径为空时不作为默认画图模式打开
11. 看图模式如果没有过缩放，则切回到画图时fittowindow
12. 看图模式未手动缩放过则在缩放时拉伸图片；允许更小的最小width/height
13. 拖入/粘贴文字变成textbox
14. 按住shift等比例缩放
15. select边框动画(灰白色间隔，蚂蚁线)
16. selectionpreview虚线框在canvas外侧部分概率性不能显示
17. 画框阴影特效
18. 标尺美化(背景半透明，竖向标尺数字不要放在刻度上)
19. 画图—>看图tab切换gif播放失效
20. ctrl+a全选功能概率性全白，概率性preview大小不对
21. (大图文件夹)imagebar快速滚动/拖动后加载机制失效，且无法点击无缩略图thumbnail(打开1k+图片可触发)(似乎好了?)
22. 大图文件夹中段文件无法打开，imagebar全白bug(似乎好了?)
23. 点开同文件夹未实际加载进tab的图片时请求直接消失bug
24. 100+图文件夹→创建未命名1，绘制→滚动到最右端→滚动回来，绘制内容消失，名字变成未命名0bug
25. 无路径启动并且加载了缓存文件时scanfolder，导致一系列bug
26. 画笔第一个点过重，且无法撤销清除bug
27. discardall无法撤销单个新建tab的bug
28. itool鼠标信息双重触发bug
29. “粘贴文字”功能读取文字的大小颜色等信息并适配
30. 无法读取中文字体(本地化)
31. 色差抠图在全屏粘贴后失效
32. 色差抠图快捷键调用方式选点机制失效
33. webp保存支持(Skiasharp)
34. win10拖动卡顿bug

Tab Paint 0.8.6:

1. 删除未命名1→生成新的未命名1→撤销→恢复未命名1，此时有2个未命名1bug（应当覆盖）
2. snipaste兼容
3. 收到剪切板图片后窗口自动弹出的设置
4. 补齐快捷键设置项
5. 设置界面快捷键特殊键名处理
6. 拖拽其他图片大选区进小图片应当拉伸小图片的画布
7. imagebar大图快速滚动长时间发白bug
8. “上次打开文件”的更改在打开原文件夹后反而消失bug
9. statusbar/toolbar随宽度显隐工具(一阶段)
10. toolbar六个工具响应式布局
11. 响应式图标自动更换，高亮显示菜单，菜单展开后后高亮选中的工具
12. svg部分彩色化
13. 找各工具鼠标图标样式(PenTool/selecttool/shapetool变成虚线十字，texttool变成I,pen(铅笔)仍为铅笔，拖拽图标换成手掌形)
14. 测试程序稳定性(win10虚拟机,初步看来没问题
15. 提取出LightTheme.xaml,并创建DarkTheme.xaml
16. 统一程序中颜色的使用到字典LightTheme.xaml
17. 通过thememanager实时响应黑暗模式变化
18. 统一图标和style到字典
19. dark-light模式切换图标变白bug
20. 鼠标指针移出canvas没恢复bug
21. 黑暗模式下设置第一次打开会有浅色标题栏bug
22. 制作安装包(绿色版几个文件+安装程序)
23. word拖拽文字崩溃(缺少资源)
24. 非法json测试（似乎没问题）
Tab Paint 0.9.0:

1. 保存只读文件崩溃退出(此时应另存为)，跟随系统设置无效
2. textbox超长文本限制(10w字)，textbox改用黑白间隙相等的边框
3. 像素化阈值默认改成160-200，删除statusbar图片详细信息按钮，
4. 看图模式隐藏标尺
5. 工具234按钮不是方形，colorselection改成深灰色边框，最右侧颜色加一根分割线
6. toolbar颜色换成两栏布局并应用样式
7. 自定义messagebox
8. 关于页面改写(多写一点，添加github主页链接，githubrelease链接检查更新(手动)，GitHub帮助界面（留空）)
9. 记忆上次关闭时的大小
10. 颜色选择界面tooltip应用默认样式，降低tooltip显示延迟
11. titlebar三按钮悬浮在windowtitle文件名上
12. 只有1张图时不允许左右切图
13. 一键制作安装包(从release目录到zip和exe全自动完成，只需填入版本号)，安装包仅在无net时下载
14. 默认主题色统一为蓝色，支持主题色设置（但不读取系统主题色）
15. statusbar隐藏顺序改成文件大小→鼠标坐标→选区→图片长宽
16. 黑暗模式bug-1:imagebar右键菜单,收起的工具选择高亮(黑)，增白黑暗模式下的悬浮滑条svg；
17. 黑暗模式bug-2:标尺文字颜色，撤销重做(黑),三个效果窗口的调整大小滑块（黑白兼有），色彩调整窗口下方三个按钮文字颜色(黑)，
18. 黑暗模式bug-3:imagetab的x(黑),，快捷键设置bar的x悬浮颜色(黑)，退出全屏模式后-O按钮颜色错误
19. applytheme递归栈溢出bug
20. 非0字节的图片格式的损坏文件打开崩溃
21. (画图模式)未手动缩放画布时改变窗口大小自动fittowindow
22. 鼠标指针颜色太淡
23. win10黑暗模式无mica后很难看的bug
24. 画图模式最小heightwidth改成刚好露出设置/thicknessbar的长宽
25. thickness-slider通过下方长度显示已有进度
26. 触控板双指左右拖动画布
27. selection在边缘拖动粘滞Bug，差数px到边缘bug
28. 画布外面的selectionpreviewhandle无法拖拽移动或改变大小
29. 每个工具一个专门的thickness-slider，专门的值和上下限
30. slider无法根据放大缩小改变长短(和黑暗模式替换有关)
31. thicknesstip切换工具后异常显示的bug
32. 编辑文本框是ctrl+c/v/a/z/y/x重定向
33. 再次加载的虚拟文件详细信息不应显示路径virtualtabpaint,应当显示内存文件
34. 看图模式切图不显示放大百分比；
35. 看图模式黑色启动时会白色闪烁一下
36. 看图模式深色背景(应用黑暗模式设计)
37. “打开新工作区”时应当关闭虚拟全新tab,非当前文件夹的所有tab,虚拟脏tab（现在都没关闭），不丢弃目标文件夹的tab,“打开新工作区”机制依旧会破坏已有缓存文件(大概率)
38. 创建未命名1234→不编辑关闭→打开→出现未命名123bug,非lastview的非当前文件夹脏旧图片不应启动时读取
39. 铅笔工具启动时紫色边框落在画刷上bug
40. 荧光笔关闭后启动，无法直接绘制，同时thicknessslider失效bug

TabPaint 0.9.1

1. 箭头工具包裹范围过大
2. delete键直接删除图片设置
3. ctrl+alt+1-3三个小工具在win10字体图标资源缺失
4. 粘贴/拖入的选区未启用裁剪按钮
5. 纯净win10虚拟机2g内存+单核cpu4k图浏览和简单编辑测试（占用300m左右，慢但最终能加载出来）+精简版win10，全新win11安装及功能测试(基本通过)
6. （安装包）net下载过慢，安装后未增加注册表，卸载后未清除user\appdata\local\缓存文件夹
7. 保存显示标尺的设置，统一两个menu样式(背景颜色，默认字号)
8. 毛刷未支持粗细bug
9. 缩放倍数输入框内按delete删除了图片（ctrl+c/x/v/a/左右同理）
10. 颜色选择增加黑白色(两个)，增加悬浮特效，增加四个主题色形成两排
11. “最近打开”未添加进列表bug，选中text工具左上角出现不明白色窗体bug（thicknesstip）
12. 屏幕取色器窗口样式优化，黑暗模式切换工作区（遮罩，文字颜色）
13. 修改imagebar关闭x的按钮颜色，选中画笔/shape高亮特效
14. 修复imagetab拖拽功能
15. 单tab时tabpaint图标可以提供tab拖拽操作
16. 指针切换bug
17. 写readme.md(学习几个热门开源项目，措辞要能多个版本不变 )

TabPaint 0.9.2

1. 裁剪选区黑暗模式bug
2. 双击打开图片裁剪图片保存崩溃bug（安装包缺少dll）
3. 支持.avif拓展名
4. **右键菜单二级菜单不能被选中，高频触发右键菜单崩溃bug**
5. 崩溃log系统
6. 双语支持(statusbar三项)+修复英语部分文字超长问题
7. win10c++2022runtime运行库提示，ocr报错提示
8. win10黑暗模式及时切换功能
9. win10右键菜单>图标
10. md5检测，ai模型重新下载
11. 双语支持修复
12. 启动时的“一次性引导,打开Welcome.png

TabPaint 0.9.3

1. 升级welcome.png(特效)
2. 项目网页（基础内容）
3. 上线到https://zouxiaofei1.github.io/tabpaint-site/
4. 帮助文档
5. 项目网页更新专用截图
6. 最小化窗口快捷键(主要用于防挡视线)，ctrl+p
7. 代码整理-3
8. 关于页重置(去掉关于TabPaint,和技术栈 居中显示，Slogan下方显示版本(0.9.xbeta),作者，颜色边框包裹，点击跳转release和主页,增加开源协议-mit协议，三个链接放到圆角矩形长按钮里)
9. 恢复出厂设置按钮样式
10. 设置六页(通用→关于)左侧加svg
11. 美化tab右键菜单样式+comboboxitem
12. tab右键贴到屏幕(移动，放大缩小，双击关闭，右键设置窗口置顶，透明度)
13. 工具长时间悬浮出现额外提示（监听剪切板，selection）
14. menu中添加创建新窗口(一级菜单，仅提供这个按钮)
15. 非selecttool时不允许ctrl+a选全图
16. textbox按delete错误提交bug
17. 英文翻译三阶段(fluentmsgbox的yesnocancel翻译，tab右键菜单，imagebar左侧三按钮tooltip,部分title,statusbar stooltip`StickTabImage的showtoast,`…)
18. thumbnail拖拽应该是复制而不是剪切?
19. 增大快捷键设置栏高度
20. 中文imeprocessed-bug
21. 增加一些格式支持
22. 单tab支持中键关闭
23. 清理拖拽缓存目录(打开设置后延时后台清理)
24. 画笔算法二阶段(加入粗细随速度改变的书写笔,马赛克性能优化)
25. 笔压感信息
26. 画刷shape左半部分支持点击(splitbutton)
27. **AI 超分辨率**
28. AI 超分辨率相关bug（16384px限制，语言文件，md5）

TabPaint v0.9.3.1

1. 画笔延迟BUg
2. 启动时默认画布改成2k
3. ai超分内存管理
4. ai超分运行在独显上
5. 根据压力板优化书写笔/压感手感-1
6. ai模型下载前弹窗确认
7. 水印工具(效果菜单,文字，图片，透明度，间距，角度设置)
8. 水印高级功能-1(样式，颜色自选，随机位置，字体自选，字体名本土化，相关翻译)
9. 水印高级功能-2（批量，性能优化）
10. 水印应用到所有功能未传入信息bug
11. 滤镜高性能优化(canvas上加一个遮罩，拖拽时改变遮罩)
12. bce窗口增加直方图
13. resize工具增加应用至所有图片功能
14. 隐藏上方5pximagebar（看图模式彻底隐藏）
15. 画笔移动实时显示实时显示画笔大小相同的圆点(颜色深浅和笔刷属性相关)
16. ai橡皮擦(需要时下载，工具栏，跑通)
17. pentool代码整理
18. ai橡皮擦(绘制速度，下载进度条，下载msgbox，本地化，下载md5,多下载源)
19. ai橡皮擦-2（红色pointer,工具图标，工具名）
20. pentool相关bug(选中铅笔橡皮时无法点击画笔，无法从设置中加载画笔栏初始icon)

TabPaint 0.9.3.2

1. settingswindow重置(fluentui风格:自定义顶栏,名字改成设置，底部去掉版本号和确定，增加=左侧栏伸缩按钮；设置更改后出现”设置已保存”的主题色1s悬浮提示)
2. settingswindow重置-2（mica特效，图标重置，禁止双击改变大小）
3. 增加几个形状工具(五角星，菱形，三角形，五边形，语言气泡)
4. 默认形状工具不应用至selecttool,按住ctrl才应用
5. welcome.png改成？，点击后弹出延迟播放的gif,下面是解说文字，左右可以改变页面
6. help窗口-2(mica特效，联网下载)
7. 滤镜(怀旧，油画，暗角，发光，和黑白反色一起放在效果-滤镜里)
8. 富文本textbox

TabPaint 0.9.3.3

1. consts文件
2. SVG作为像素格式打开及bug修复
3. richtextbox粘贴大小适应
4. richtextbox增加表格编辑，文字阴影高亮，特效，上下标
5. 水印增加右侧预览窗口，增加mica和缩放改变大小
6. tts/bce窗口合并成一个，增加右侧预览窗口
7. 直方图bug修复
8. 马赛克，高斯模糊，锐化，褐色滤镜
9. 设置网页打不开bug
10. 检查新版本(打开设置时showtoast)
11. 读取ICC 配置文件校色(校准图片显示，作为一个设置选项，SkiaSharp)
12. 非矩形(套索)选区-1UI实现
13. 非矩形(套索)选区实现(没有周围8个handle,不允许放大缩小)
14. 魔棒工具
15. 高级选区相关bug(delete删除为矩形区域，拖拽判定为矩形区域)

Tab Paint 0.9.4

1. 代码整理-1
2. 另存为ico
3. 设置-高级-打开缓存文件夹按钮
4. 高斯模糊画刷
5. AdjustColor,WaterMark,Resize窗口效果更新
6. imagebar展开收起(双击bar空白或者点击“放弃所有更改”下面按钮)，文字左侧紧凑排列，右边x左边红色点
7. imagebar形态bug-1(双击后imagebar高度没有立刻改变)，缩回后左侧按钮消失，红点缩回后消失
8. 悬浮在缩回的imagetab上显示预览图
9. 恢复imagebar右键菜单
10. 设置项记忆展开缩回状态
11. imagebar形态-2(右键菜单文字和图标，切换按钮改成上下三角)
12. svg解析最低像素不低于512px
13. 一个悬浮在窗口中下部，半透明的下载ai模型进度条（statusbar上，极简风,可拖动和关闭）
14. 黑暗模式水印窗口文字
15. 剩余除设置外窗口-1（ico,moderncolorpicker,helpwindow)
16. fluentmessagebox(左侧svg,很淡的mica,全窗口可拖动)
17. 增加一些颜色(→100，透明度>50显示透明x,>95显示透明)
18. tts/bce窗口拖动条变成彩色
19. 提取常量-2
20. 默认加载ico最大一帧

Tab Paint 0.9.4.1

1. helpwindow切图闪烁
2. 水印窗口文字本地化
3. colorpicker黑暗模式bug
4. colorpicker-tooltip被遮挡bug
5. 黑暗-浅色模式切换后mica消失bug
6. imagetab-previewimage下方显示尺寸，大小，悬浮超过1s加载清晰大图
7. 设置/loadimage拆分
8. 铅笔光标错误bug
9. 折叠后拖拽区域更新
10. 剩余除设置外窗口-2(colorpicker,,filterstrength）
11. 字体combobox-slider样式（水印combobox-slider同）；tooltip显示延迟
12. filterstrength美化，点击外部焦点未消失bug
13. 选区select创建后上方显示复制/ai抠图/ocr按钮；//移动/改变大小时不显示?
14. 选区selectai抠图bug
15. commit到网站-1
16. 启动速度优化(canvas右键菜单等)-1
17. quickbenchmark缓存评分结果和上次评分日期，每月重新评分一次+loaded延迟执行
18. Icons.xaml在ApplyTheme和ContentRendered重复加载?
19. 延迟加载`InitializeLazyControls`basiccolorgrid

Tab Paint 0.9.4.2

1. 报错体验优化(fluentmessagebox,点击打开文件夹)
2. 延迟加载DownloadProgressFloat,selectiontoolbar
3. icon.xaml分步加载拆分成两个
4. 启动时setzoom被调用3次bug
5. 折叠imagebar启动后fittowindow失败
6. 启动速度优化(-200ms)
7. SettingsManager延迟加载

Tab Paint 0.9.4.3

1. 默认折叠imagebar启动/默认不扫描文件夹
2. 中键关闭图片后预览图仍然存在bug
3. 多窗口bug修复-1（多数，如新窗口选区不能拖动,工具不能切换到非画笔类工具(pentool)主窗口的设置/,新窗口上切换画笔颜色，按钮颜色更新在主窗口上,有时候不能绘制,canvas周围的八个resizehanlde显示不出来…）
4. 170%-300%放大卡顿bug
5. dragoverlay失效
6. splitbutton样式优化
7. 套索复制粘贴
8. 套索工具栏美化,小尺寸select不显示工具栏
9. previewtabimage对于透明图片显示灰白格子，去除新图片loading，加载前显示宽高
10. 套索抠图
11. 套索第二次拖动意外commitbug
12. 看图-画图-看图切换后imagebar折叠的排版失效bug
13. slider最短长度，左右拖拽条失效
14. 子窗口能卡住新窗口（设置，水印）
15. 子窗口能卡住新窗口bug-2(colorPicker,调整图像，滤镜强度，ico保存)
16. 窗口样式(ico保存黑暗模式，水印-设置-字体颜色，调整图像窗口)
17. 样式(右键菜单-圆角剩余边，win10fluentmessagebox圆角剩余边，汉化expand/collapsetools，自定义颜色虚线颜色加深)
18. 选中时systemaccentcolor边框
19. 最初工具使用25px的圆brush
20. 看图模式显示了笔刷圆圈bug
21. win10噪点渐变背景
22. (win10)样式不为none的子窗口右上三按钮无法显示；
23. fluentmessagebox卡住新窗口bug
24. 图片收藏夹（statusbar右侧，五角星图标，点击在下方弹出窗口，多行imagebar,支持外部往里拖拽/拖拽到画布）
25. ai橡皮提示改成第一次切换到工具时
26. ,对于新图片显示正确的宽高(而不是小图片)，当前tab不显示
27. 关闭一个窗口全程序退出bug
28. 收藏夹改进-1(取消收藏窗口的顶部栏,左侧栏,分页，单行，提供一个+按钮添加图片)
29. 看图模式关闭高亮边框
30. 子窗口select无法拖拽
31. 拖拽imagetab到屏幕外面->创建屏幕下十分之一的悬浮窗口，阴影遮罩(显示拖拽到此处创建新窗口，松开消失，上方什么都没有，拖拽到上方十分之九沿用原有逻辑(复制文件等))→拖拽到悬浮窗创建新窗口(携带这个tab和其撤销栈)
32. 跨页撤销栈
33. 切换主题颜色后toolbar-splitbutton外框和statusbar放大镜图标颜色未更新

Tab Paint 0.9.4.4

1. win10-atlas精简版样式功能测试，补充语言文件
2. 窗口间select选区拖拽
3. imagebar-slider图片少时概率性不禁用
4. 副窗口不重叠于主窗口时无法移动选区bug
5. 空白图fittowindow失败
6. 第一次启动时语言为系统语言
7. imagebar-悬浮的previewimage对于ico显示最大一帧的px;悬浮超过0.5s后gif播放闪烁bug
8. 设置-插件(提供ai功能的安装和卸载)
9. win10黑暗模式即时切换
10. 设置-插件下载进度条美化
11. imagetab关闭按钮始终显示的设置
12. 跨页撤销栈内存占用优化
13. 微调窗口样式-1
14. 选区工具标尺高亮
15. filetabitems-文件回收站窗口，默认7天保存期，右侧网格状排版的图片（100px*80px）,网格状图片左上角显示剩余日期，右上角点击恢复，右下角点击删除
16. 所有快捷键tooltip和设置实时绑定
17. 不允许设置两个相同的快捷键
18. 修复收藏夹磁吸，改进样式-1
19. 代码整理
20. filetabitems缓存机制失效（打开图片，绘制，关闭，重启，图片消失）
21. imagebar-popup原图读取修改过的(如果有)
22. 多窗口图片互斥
23. 黑暗模式切换(dwmborder，ruler)
24. 撤销栈缓存大小textbox按钮样式
25. imagetab拖进其他窗口的tab
26. session.json改成bin
27. zoomslider拖动没有效果
28. 90旋转选区没有updatecanvas?
29. 新窗口错开创建
30. 全局撤销计数
31. 设置未及时保存Bug
32. 收藏窗口-使用150px的小图增加加载速度；
33. 拖入超过16384px图片卡死bug
34. 允许滚动到画布外面
35. 第一次缩放前不显示滚动条

TabPaint 0.9.5

1. fluentmessagebox设置左边栏背景丢失bug
2. xxmb控件美化(左右大小相同，强调色出现在左侧
3. 收藏窗口bug-允许主动磁吸，磁吸到其他三边
4. 关闭图片→重启→图片仍显示bug
5. 创建新窗口，两个窗口都有未命名1，可能导致缓存文件冲突；未命名字符串翻译
6. 关闭新标签页缓存文件残留bug
7. 撤销栈内存压缩？
8. 恢复快捷键绑定
9. 收藏窗口美化-1（mica背景，拖动不要改变favoritewindow布局(改变吸附边)，类抽象化）
10. 收藏窗口-2（右侧+动画，多个窗口仅允许创建一个favoritewindow,允许吸附到多个mainwindow）
11. favoritewindow打开时，关闭所有主窗口程序也不退出bug
12. imagebar-tooltip只在展开时显示
13. UpdateTabThumbnail多线程优化
14. imagetab性能测试及优化
15. 笔刷优化性能
16. 马赛克，高斯模糊改成连续性笔刷，支持1000px直径
17. loadimage-SVG性能优化
18. 高清预览图缓存(300px,5张)
19. aiservice模型驻留
20. 画笔优化-2（性能，书写笔，blur样式）

Tab Paint 0.9.5.1

1. 收藏窗口-3（相对位置，左侧图标，左侧菜单双击允许重命名，x图标)
2. dwmaccentcolor应用至程序中每一个窗口(画图模式下)
3. 程序内文本finetune（插件-安装改成管理，yesnocancelmsgbox,减少设置栏desc文字量，补齐imagebartooltip）
4. 标尺左上角美化(改成纯色)，画笔不隐藏十字光标
5. 画布resize虚线框样式修改
6. 缩放闪现Bug
7. imagetab跨窗口拖动大概率出现两个tab
8. imagetab跨窗口拖动改动丢失
9. 内存文件imagebar右键菜单不显示“打开新文件夹”和“删除文件”
10. 初次启动提示
11. 水印只应用了10张bug,且imagebar滚回后更改消失
12. 批量处理进度条（保存，resize）
13. saveall只保存filetab文件bug
14. 调整大小窗口点击确定无反应bug
15. downloadprogressbar改名
16. 水印resize滤镜性能优化？
17. 性能优化(油画滤镜)
18. 新窗口-》打开图片→批量应用水印→报错bug
19. 监听剪切板互斥
20. 修改过的图片往新窗口拖拽-》切换到新窗口别的图片-》切回来→修改丢失bug
21. 不同窗口画笔粗细透明度参数独立
22. 触控板上下左右滑动移动画布速率不等
23. 收藏夹触控板双指拖动,相对位置不变
24. 收藏夹维护自定义排序；支持拖动排序

Tab Paint 0.9.5.2

1. favorite和主窗口吸附时在一层级
2. 收藏窗口美化
3. l_imagebar_newimage缺失，设置combobox调整，颜色框自定义颜色padding调整
4. 创建新图片→直接左右键切图→全部变成dirtybug
5. 图片加载为预览图阶段时画刷图标特别大
6. 毛刷无论怎么涂抹都涂不满bug
7. 跨窗口拖动选区后源窗口selectionfloat残留
8. session.bin累积
9. tab拖拽后updateui，
10. 拉出选框→不移动，点击外面→画布变dirtybug
11. 魔棒性能优化
12. 魔棒容差slider
13. ai擦除后图片模糊bug
14. esc取消魔棒选区
15. 超分taskprogressfloat进度条
16. TimeSpan overflowed because the duration is too long报错
17. 插件与ai模型文本Bug（安装-管理）
18. 未命名1重名
19. 画笔转圈绘画卡顿，呈现多边形形状
20. 创建新窗口时toolbar选中源窗口选中的工具；最后一个关闭的窗口向设置中写入工具设置
21. 魔棒容差slider美化(改成一行，combobox提供几个常用数值选择，布局不要重叠到一起)
22. explorer右键程序图标点击关闭后仍运行
23. 魔棒容差slider实时应用结果
24. selectiontoolbar增加一个旋转按钮，点击后下方出现一个fluentui风格的弧形悬浮窗显示角度，拖拽可以旋转选区，结果实时显示
25. 选区旋转float放在select框下方跟着移动；角度数值框fluentui应用
26. 旋转选区->拖动选区->旋转选区->此时选区回到原来位置bug
27. 选区旋转toggle，开关状态显示在selectiontoolbar上，并作为设置记忆
28. canvasresize的线改成黑白色间隔的线
29. canvasresize时statusbar上的图片大小及时更新
30. 下载进度条/welcome文字颜色
31. 调整画布大小窗口拖动滑动条后主窗口实时预览效果
32. 选区粘贴位置为视图左上角
33. 选区旋转后同样旋转选框和handle，旋转后拉伸选区斜向拉伸
34. 旋转选区后拉伸图片报错bug,缩小后图片错位
35. 旋转选区→commit→创建新选区时出现原有选区框残留
36. moderncolorpicker的关闭按钮左边增加一个<按钮，点击后变成一个小的色轮选色窗口，提供色轮，主次颜色选择，拖动时实时和窗口颜色同步，提供吸附逻辑，fluentui风格
37. 渐变色画刷

Tab Paint 0.9.5.3

1. menubar中放弃所有更改按钮改成放弃这张图片更改
2. 方形画刷连续性
3. win10自行显示圆角矩形，窗口阴影?
4. shapetool通过ctrl切换专业模式
5. shape专业模式（切换时showtoast显示提示，鼠标悬浮在handle上时改变光标形状，改变slider时实时应用）
6. shape专业模式下旋转，shift等比例缩放
7. （shape专业模式）创建shape→右下handle不断向左上拉→意外改变了左上handle位置bug
8. （shape专业模式）handle大小不应随画布缩放改变，预览和应用略微错位
9. 选区拖拽到屏幕下方十分之一，以选区为tab创建新窗口(复用)
10. 笔刷大小预览圆颜色:（荧光笔黄色，其他笔用画笔颜色）
11. 滤镜新增:消除红眼，铅笔素描，边缘检测
12. selecttool旋转角度工具栏不应随方框旋转上下移动
13. 矩形旋转工具性能优化，选区旋转工具撑大画布bug
14. 支持旋转的选区发送到新窗口
15. 放弃单张未命名图片更改后图片消失（应该创建一个空白的同名图片），未更新thumbnail
16. shapetool-alt专业模式开关即时切换
17. select菜单在选区大小>可视区域0.05时才显示；标尺选区强调色两端用更深的1px主题色标记(隔断)
18. colorpicker小窗口状态（去掉饱和度滑条，一二颜色选择和亮度滑条放一行，窗口变更小，colorpicker出现时可以选择主窗口）

TabPaint 0.9.5.4

1. Copycolorcode失效bug
2.  0.9.5.4一个封包缺少Microsoft.Windows.SDK.NET.dll bug
3. 和剪切板记录软件(Clibor)冲突bug？
4. readme.md重置
5. 旋转非96px图片，jpeg后保存数据丢失bug
6. 中英文切换子菜单不能及时更新bug

TabPaint 0.9.5.5

1. 看图模式<,>按钮
2. selection部分拖出canvas后ruler高亮消失bug
3. 设置-通用增加”添加到系统右键菜单”check
4. 收集系统报告窗口
5. 收集系统报告窗口美化(默认过滤日志中的info信息,自定义titlebar)
6. 设置增加重启后放弃所有更改（包括缓存文件，新图片和imagebar）
7. 设置-重启后放弃所有更改没能写入设置文件bug
8. whatsnew窗口

TabPaint 0.9.5.6

1. selecttool切换到别的工具后commit而不应该giveup选区
2. esc取消所有选区
3. 不拖动实时更新ai抠图界面
4. tabpaint已启动→以tabpaint为打开方式打开新图片时窗口未获得焦点bug
5. shift+tab展开/收起imagebar(可设置)
6. 旋转选区→发送到新窗口→源窗口创建新选区时preview为空白bug
7. 创建选区→放大→旋转→选区变成原来的大小bug
8. 反馈窗口更改样式(放到设置窗口内，包括程序设置，imagebar中图片信息)
9. 处于设置-收集报告/ai模型管理页面时，左边栏显示相应的图标
10. 关于页面mit协议，点击后出现隐藏页面
11. session中保存了某张image→explorer中打开这张image→imagebar中出现两个这张image
12. 选区旋转由floatbar改成刻度条
13. 竖直ruler数字能显示到statusbarbug
14. 设置-协议页面协议折叠时的三条要点没有显示bug
15. about下方合并成一个frame，增加更新日志，建议反馈，首页
16. 日志生成异步化，增加动画

TabPaint 0.9.5.7

1. 贴图滚轮放大缩小以鼠标为中心，窗口边角缩放贴图窗口(但不改变比例)；，右上角增加x
2. performancescore应用到滤镜，画笔
3. 日志写入上限(每个文件最多1m)
4. 窗口置顶(canvas右键菜单，设置持久化)
5. 设置中已安装的ai插件显示占用空间
6. paddleocr(下载进度条，ai模型管理，下载前确认)
7. paddleocr支持
8. newestinstalledversion项防重复触发
9. 支持acrobat选区拖拽到自己程序?
10. (ocr)悬浮层标注文本地点可部分复制，floatbar显示(复制所有文本，确定)
11. (ocr)悬浮层显示设置
12. aiocr特效
13. 恢复出厂设置进度条
14. ai插件窗口内鼠标滚轮上下滚动没反应
15. 贴图出现时，关闭主窗口不导致程序退出,右上角增加x按钮
16. 贴图出现主窗口关闭时出现托盘，双击托盘窗口可恢复+托盘右键菜单
17. win10旧版(无 [WebP Image Extensions](https://apps.microsoft.com/detail/9pg2dk419drg))支持webp
18. 连续点击多个插件下载按钮有时会出现not installed+灰色的install按钮的状态锁死bug（重启去除）
19. 右键trayicon不显示菜单bug
20. 设置-插件-一键下载
21. 托盘菜单美化（fluentui风格，上方显示较大的tabpaint图标，下方两个按钮）
22. win10采样桌面背景图片作为仿mica

TabPaint 0.9.5.8

1. 长按看图←→按钮快速切换图片
2. ctrl+shift+v创建新页面时，giveup当前未commit的选区
3. 贴图窗口增加（复制，全部关闭），拖拽图片进入贴图窗口生成新的贴图窗口，贴图-复制改成复制文件，ctrl+c复制支持
4. 连续点击看图模式tabpaint图标创建菜单，第一次位置正常，第二次及之后创建在屏幕左上角bug
5. “放弃这张图片更改”右键点击出现放弃所有图片更改menu
6. 禁止看图模式下向窗口内拖拽titlebar-logo功能；禁止看图模式下canvas右键菜单
7. 贴图窗口增加x按钮描边阴影，窗口阴影 
8. 看图模式下允许窗口部分拖动到屏幕外面
9. 设置-看图设置-鼠标滚轮功能增加上下滚动图片，titlebar-logo菜单增加“设置”
10. 右键菜单鸟瞰图显示选择
11. moderncolorpickerwindow无法选中主窗口bug
12. 右键tab(退出→关闭)(文字错误)，关闭其他标签页
13. 窗口失去焦点后隐藏imagebartab预览图
14. 双击statusbar命令行

 TabPaint 0.9.6.0

1. ctrl+p打印
2. 合并为pdf
3. 长按ctrl+l(4k+)图，窗口直接卡住
4. 看图模式单图不显示<>,切到最左/右循环时图标显示特效
5. 贴图窗口不应允许贴边改变大小，画布周围padding改成200px→50px
6. 水印-图片-缩放文字颜色bug
7. 看图模式拖动条无法拖拽（变成拖拽画布bug）
8. 竖向ruler像素不对齐；旋转后横向不对齐bug
9. 点击贴图时选区贴图支持selection
10. 设置增加跟随系统主题色
11. 收集报告窗口右下角白色方框bug
12. 较长比例图片色彩调整菜单预览显示bug
13. 缩小的colorpicker在水印窗口上无法commit颜色bug
14. 水印预览不符合实际Bug
15. 打印窗口显示”此应用不支持打印预览”bug
16. 箭头无法从右向左画bug
17. shapetool图形较小时（三角形菱形五边形）commit后边缘截断
18. favoritewindow不同颜色标识不同页
19. / 展开命令行，输入prompt回车后ai画图，设置-画图内另开新页配置api
20. 旋转翻转时保证视窗不变
21. 小窗口打开的colorpicker无法关闭bug
22. shapetool三角形菱形五边形五角星移动距离很小时变成长线bug，矩形-箭头commit后比commit前的预览图大
23. 新建图片预览图px显示错误bug
24. colorpicker窗口-/+，缩小时去掉tabpaint图标
25. 第一次切换至看图模式显示“点击图标显示菜单”hint
26. alt切换selecttoolfloatbar显示与否，默认不显示
27. texttool不存在方框时resizehandle也会隐藏bug

TabPaint 0.9.6.1

1. 像素绘画定时撤销记录
2. 编辑ico按ctrl+s时不弹出保存ico保存选项对话框，而是直接按照现有的ico格式保存
3. textbox拖拽后，内部闪烁的光标消失，但是之后无论怎么点击textbox内部，光标也不再出现很难再编辑文字，textbox拖拽困难（部分解决）
4. subwindowstyle输入框下方强调线和边框有1px空隙，最近打开菜单样式调整
5. tabpaint图标菜单显示鼠标滚轮功能切换
6. 关于（开发者：github主页，ver:检查更新）
7. 快捷键录入框点击后显示”等待录入”,按键实时显示，点击录入框外面则放弃这次录入
8. RunInferenceAsync,RunInpaintingAsync支持按esc静默取消，ai推理时不显示floatbar
9. 直线左下→右上画方向错误bug

TabPaint 0.9.6.2

1. ai擦除时切图
2. 模型保存位置设置
3. 增加ctrl+q格式快速调整面板(空心圆圈，webp,jpg,png,bmp,ico五等份,fluentui风格)按下ctrl+q后面板出现，按q则顺时针旋转；松开ctrl则将当前图片转成这个格式，源图片删除；被选中的高亮；移动鼠标选中部分向着鼠标方向否则键盘控制；点击可提交；快捷键可设置，复用已有样式和语言
4. 格式调整面板增加至快捷键设置
5. 格式调整面板文件覆盖bug
6. 多语言toast
7. 格式调整面板样式调整
8. 部分图片webp编码器bug
9. rmbg2.0模型设置
10. rmbg2.0样式修改
11. rmbg模型切换时卸载模型
12. rmbg2.0低配使用时显示警告
13. 看图隐藏命令行
14. 开发者模式
15. 新建支持指定大小

TabPaint v0.9.6.3

1. win10背景不随拖动改变
2. emoji-1设置-最大化x,检查更新，帮助文档，)
3. `mw.TaskProgressPopup.SetIcon从emoji改成svg`
4. 支持透明的格式转成不支持透明的showtoast”透明度可能丢失”
5. emoji-2二级菜单>(滤镜，最近打开，右键菜单)
6. textbox专业模式(旋转)-1
7. 文案调整-3
8. textbox专业模式旋转错位bug
9. texttool旋转→输入→文本框大小变化，文字位置错位bug
10. 图标修复:textfloatbarH,<,>,自动色阶，水印，展开/收起,贴图窗口20-100，全部关闭，复制
11. 创建选区→拖动→旋转→放大此时旋转预览出现错位Bug
12. 旋转的选区跨图片发送后预览框错位bug
13. selecttool专业模式创建选区，鼠标抬起时选区闪烁一下
14. shapetool提交前后大小不符
15. shapetool非黑色时出现灰黑色边
16. 改变窗口大小时不相关重排（如上下拉伸，titlebar-ruler重绘）
17. shapetool增加和selecttool一样的ruler蓝色特效
18. shift/ctrl多选tab
19. 多选tab-导出pdf,拖出到桌面

TabPaint v0.9.7.0

1. 触控板左右双指滑动imagebar
2. 打开数百张图→打开最右边一张→向右切图，跳到最左边→title上仍然显示(几百/几百)而不是(1/几百)bug
3. rmbg使用独显?
4. textbox内部拖拽bug
5. tab右键菜单增加(将标签页移动到新窗口/右侧新建标签页)
6. imagetab当图片数量多时宽度自动减小(仅收起模式)
7. 焦点在设置-通用主题色按钮上时，上下左右键/wasd切换主题色
8. 旋转的选区允许保留角度拖入新图片
9. 旋转条手感
10. github配置issue7天无回复自动关闭
11. 鼠标在imagetab上按下→点击tab变成看图模式时，有imagebar残留(鼠标抬起后消失)
12. 形状工具专业模式/texttool→切换工具→改成commit到画布而不是giveup
13. 二维码识别
14. 二维码识别拖拽/粘贴触发
15. 支持设置-插件中手动下载zxing.net依赖实现二维码识别()，程序包不自带
16. 动态加载二维码识别bug
17. 设置-插件中手动下载zxing.net，之后不会自动加载；卸载时被程序自己占用无法删除
18. 设置-插件中卸载dll时被自己占用删除失败
19. win10背景调整，聚焦/失去焦点用不同颜色标识
20. 设置-高级-颜色校准切换时，刷新当前加载的图片
21. 设置通用主题色改成:主题色放在预设颜色上面单独一行， 一个长条形方框，系统主题色的色调，四等分，各区域颜色从左到右由浅到深，可选中任何一个
22. “回到第一张图片”“这是最后一张图片”toast15s内只能触发一次
23. shapetool三角形/菱形/五边形/五角星旋转时八个handle位置异常bug

TabPaint 0.9.7.1

1. title/menubar合并布局-1
2. 合并布局实现图标/menu响应式
3. 合并布局bug修复
4. 鼠标悬浮在最近打开列表条目时，显示这个条目的信息
5. 鼠标悬浮在最近打开列表时，若文件不存在明确显示不存在
6. 400px高度以下用合并布局
7. 高级可设置最近打开列表记录条目数
8. 设置窗口出现且聚焦于设置窗口时，拖拽图片进入主窗口区域不要触发插入图片/添加到列表/切换工作区
9. psd读取
10. psd读取插件联网下载
11. 设置中临时页面(如插件页)右键点击弹出菜单，允许固定这个页面；设置持久化；被固定的页面左边栏图标右上角增加Pin_Image图标
12. magick dll大小计算方法错误
13. psd读取bug修复

TabPaint 0.9.7.2

1. 触屏设置页上下拖动支持
2. 触屏画布双指放大手势支持
3. 点击hex(8位)可以在hex(8位)/（6位)之间切换
4. svg解码大小设置
5. 按住shift等比例缩放选区
6. explorer框选打开多张图片→实际仅打开一张bug
7. 已打开tabpaint→双击tabpaint主程序图标→tabpaint窗口未获得焦点bug(应该开一个未命名新窗口)
8. list已有图片→explorer打开相同图片→图片显示两张bug
9. 设置-高级增加启用pdf保存页面的设置,默认打开
10. shapetool的handle会随放大缩小画布而改变bug（未及时更新）
11. 鼠标悬浮在title上显示工作目录，右键清空工作目录
12. pdf保存页面(fluentui风格，样式类似于icoexportwindow但是更大，左侧显示图片可禁用，右侧显示预览，预览可上下滚动)，仅设置中开启时启用
13. pdf保存页面逻辑修改（顶部栏，拖动，禁用）
14. pdf保存页面非阻塞式
15. pdf保存页面美化