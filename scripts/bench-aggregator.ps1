param(
    [string]$Url = "http://localhost:5000/api/aggregator/orders/1/full",
    [string]$Label = "HTTP baseline"
)

1..3 | ForEach-Object { Invoke-RestMethod $Url | Out-Null }   # прогрів

$times = 1..20 | ForEach-Object { (Measure-Command { Invoke-RestMethod $Url | Out-Null }).TotalMilliseconds }
$m = $times | Measure-Object -Average -Minimum -Maximum
"{0}: avg {1:N1} ms, min {2:N1} ms, max {3:N1} ms" -f $Label, $m.Average, $m.Minimum, $m.Maximum