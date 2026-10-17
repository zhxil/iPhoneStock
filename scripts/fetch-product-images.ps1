param([string]$HtmlDirectory = 'artifacts/ui-v13', [string]$Python = 'python')
$ErrorActionPreference = 'Stop'
& $Python -c 'from PIL import Image'
if ($LASTEXITCODE -ne 0) { throw '重新获取图片需要 Python 和 Pillow；运行程序不需要它们。' }
$root=Split-Path -Parent $PSScriptRoot
$assets=Join-Path $root 'native/Stock.WinUI/Assets/Products'
New-Item -ItemType Directory -Force $assets|Out-Null
$catalog=Get-Content (Join-Path $root 'native/Stock.Core/Data/products.json') -Raw|ConvertFrom-Json
$entries=[Collections.Generic.List[object]]::new()
foreach($family in $catalog|Where-Object category -eq iphone){
 $page="https://www.apple.com.cn/shop/buy-iphone/$($family.family)"
 $htmlFile=Join-Path $root "$HtmlDirectory/apple-$($family.family).html"
 if($family.family -eq 'iphone-duo'){$htmlFile=Join-Path $root "$HtmlDirectory/apple-duo.html"}
 $html=if(Test-Path $htmlFile){Get-Content $htmlFile -Raw}else{(Invoke-WebRequest $page).Content}
 foreach($group in $family.data.products|Group-Object familyType,dimensionColor){
  $p=$group.Group[0];$color=$p.dimensionColor
  $model=switch($p.familyType){'iphone18pro'{'iphone-18-pro'}'iphone18promax'{'iphone-18-pro-max'}'iphoneduo'{'iphone-duo'}'iphoneair'{'iphone-air'}default{$p.familyType -replace '^iphone','iphone-'}}
  $imageColor=switch($color){'nightsky'{'night-sky'}'starwhite'{'star-white'}'soft_pink'{'softpink'}default{$color}}
  $date=switch($family.family){'iphone-18-pro'{'202609'}'iphone-duo'{'202609'}'iphone-17e'{'202603'}'iphone-16'{'202409'}default{'202509'}}
  $name=if($family.family -eq 'iphone-16'){"$model-$imageColor-select-$date"}else{"$model-finish-select-$imageColor-$date"}
  $pattern='"'+[regex]::Escape($name)+'":\{"sources":\[\{"srcSet":"([^"]+)"'
  $match=[regex]::Match($html,$pattern)
  if(!$match.Success){throw "Missing exact image: $name"}
  $url=[Net.WebUtility]::HtmlDecode($match.Groups[1].Value)
  $file="$model-$color.png"
  Invoke-WebRequest $url -OutFile (Join-Path $assets $file)
  foreach($item in $group.Group){$entries.Add([ordered]@{partNumber=$item.partNumber;file=$file;color=$family.data.displayValues.dimensionColor.$color.value;source=$page;imageUrl=$url})}
  Write-Output "$model / $color"
 }
}
$entries|ConvertTo-Json -Depth 4|Set-Content (Join-Path $assets 'index.json') -Encoding utf8
& $Python (Join-Path $root 'scripts/index-product-bounds.py')
if ($LASTEXITCODE -ne 0) { throw '商品图边界索引生成失败' }
