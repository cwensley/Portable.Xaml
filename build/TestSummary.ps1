<#
.SYNOPSIS
    Renders trx test results into a GitHub Actions job summary.

.DESCRIPTION
    Reads the trx reports written by `dotnet test --logger trx` and appends a markdown summary to
    $env:GITHUB_STEP_SUMMARY - a row per test assembly and framework, a total, and a collapsible
    entry per failed test. Failures are also emitted as workflow error annotations, which surface
    them on the PR diff when the stack trace points at a file in the repository.

    Writes to stdout when run outside of Actions, so it can be tested locally:
        pwsh build/TestSummary.ps1 -ResultsRoot artifacts/test
#>
[CmdletBinding()]
param(
	# Heading for the summary.
	[string]$Title = 'Unit tests',
	# Directory searched (recursively) for trx reports.
	[string]$ResultsRoot = 'artifacts/test',
	# Name/pattern of the trx report(s) to read.
	[string]$FileName = '*.trx',
	# Cap the per-test detail so a badly broken run can't blow the 1MB summary limit.
	[int]$MaxFailures = 50,
	# GitHub only displays the first handful of annotations per step, so don't bother emitting more.
	[int]$MaxAnnotations = 10
)

$ErrorActionPreference = 'Stop'

$summary = [System.Text.StringBuilder]::new()
function Add-Line([string]$text = '') { [void]$summary.AppendLine($text) }

# Fenced code blocks are used for failure details, so make sure the content can't end one early.
function Format-Block([string]$text)
{
	if ([string]::IsNullOrWhiteSpace($text)) { return $null }
	return $text.Replace('```', "'''").TrimEnd()
}

function Format-Outcome($failed, $total)
{
	if ($failed -gt 0) { "❌ $failed failed" } elseif ($total -eq 0) { '⚠️ no tests ran' } else { '✅ passed' }
}

$files = @(Get-ChildItem -Path $ResultsRoot -Recurse -Filter $FileName -File -ErrorAction SilentlyContinue | Sort-Object Name)

Add-Line "## $Title"
Add-Line

# Everything is buffered and only written at the end, so report any error into the summary itself
# rather than leaving no summary at all.
try
{

if ($files.Count -eq 0)
{
	Add-Line '> [!WARNING]'
	Add-Line "> No test results found under ``$ResultsRoot`` - the test host didn't write a report. See the job log."
	Write-Output "::warning title=$Title::No test results found, the test host didn't write a report"
}
else
{
	$ns = @{ t = 'http://microsoft.com/schemas/VisualStudio/TeamTest/2010' }
	$suites = [System.Collections.Generic.List[object]]::new()
	$failures = [System.Collections.Generic.List[object]]::new()

	foreach ($file in $files)
	{
		# A trx from a host that died mid-write is empty or truncated; record it and keep going.
		try
		{
			$content = Get-Content -LiteralPath $file.FullName -Raw
			if ([string]::IsNullOrWhiteSpace($content)) { throw 'The file is empty.' }
			$xml = [xml]$content
		}
		catch
		{
			Add-Line '> [!WARNING]'
			Add-Line "> ``$($file.Name)`` could not be read, so its results are missing below. $(($_.Exception.Message -replace '\s+', ' ').Trim())"
			Add-Line
			Write-Output "::warning title=$Title::$($file.Name) could not be read"
			continue
		}

		# The file name doesn't say which project it came from, so label it from the test assembly
		# path, e.g. ...\Tests.System.Xaml\Release\net48\Tests.System.Xaml.dll -> Tests.System.Xaml (net48).
		$name = $file.BaseName
		$method = Select-Xml -Xml $xml -Namespace $ns -XPath '//t:TestDefinitions/t:UnitTest/t:TestMethod' | Select-Object -First 1
		if ($method -and $method.Node.codeBase)
		{
			$assembly = $method.Node.codeBase
			$name = "$([IO.Path]::GetFileNameWithoutExtension($assembly)) ($(Split-Path -Leaf (Split-Path -Parent $assembly)))"
		}

		$suite = [pscustomobject]@{ Name = $name; Total = 0; Passed = 0; Failed = 0; Skipped = 0; Seconds = 0.0 }
		$counters = Select-Xml -Xml $xml -Namespace $ns -XPath '/t:TestRun/t:ResultSummary/t:Counters' | Select-Object -First 1
		if ($counters)
		{
			$c = $counters.Node
			$suite.Total = [int]$c.total
			$suite.Passed = [int]$c.passed
			$suite.Failed = [int]$c.failed
			# VSTest leaves notExecuted at 0 for ignored tests, so anything that didn't pass or fail is skipped.
			$suite.Skipped = $suite.Total - $suite.Passed - $suite.Failed
		}

		$times = Select-Xml -Xml $xml -Namespace $ns -XPath '/t:TestRun/t:Times' | Select-Object -First 1
		if ($times -and $times.Node.start -and $times.Node.finish)
		{
			$suite.Seconds = ([datetime]$times.Node.finish - [datetime]$times.Node.start).TotalSeconds
		}
		$suites.Add($suite)

		# testName is only the method, so pull the class name from the definitions.
		$classNames = @{}
		foreach ($def in (Select-Xml -Xml $xml -Namespace $ns -XPath '//t:TestDefinitions/t:UnitTest'))
		{
			$m = $def.Node.TestMethod
			if ($m) { $classNames[$def.Node.id] = $m.className }
		}

		foreach ($result in (Select-Xml -Xml $xml -Namespace $ns -XPath "//t:UnitTestResult[@outcome='Failed']"))
		{
			$node = $result.Node
			$class = $classNames[$node.testId]
			$failures.Add([pscustomobject]@{
					Suite      = $name
					Name       = if ($class) { "$class.$($node.testName)" } else { $node.testName }
					Message    = Format-Block $node.Output.ErrorInfo.Message
					StackTrace = Format-Block $node.Output.ErrorInfo.StackTrace
				})
		}
	}

	Add-Line '| Tests | Result | Total | Passed | Failed | Skipped | Duration |'
	Add-Line '|:---|:---|---:|---:|---:|---:|---:|'
	foreach ($s in ($suites | Sort-Object Name))
	{
		Add-Line "| $($s.Name) | $(Format-Outcome $s.Failed $s.Total) | $($s.Total) | $($s.Passed) | $($s.Failed) | $($s.Skipped) | $([math]::Round($s.Seconds, 1))s |"
	}
	if ($suites.Count -gt 1)
	{
		$sum = { param($p) ($suites | Measure-Object -Property $p -Sum).Sum }
		$failed = & $sum Failed; $total = & $sum Total
		Add-Line "| **Total** | **$(Format-Outcome $failed $total)** | **$total** | **$(& $sum Passed)** | **$failed** | **$(& $sum Skipped)** | **$([math]::Round((& $sum Seconds), 1))s** |"
	}
	Add-Line

	$shown = 0
	foreach ($failure in $failures)
	{
		if ($shown -ge $MaxFailures)
		{
			Add-Line "_...and $($failures.Count - $MaxFailures) more, see the job log._"
			break
		}
		$shown++

		Add-Line "<details><summary>❌ <code>$($failure.Name)</code> - $($failure.Suite)</summary>"
		Add-Line
		Add-Line '```'
		if ($failure.Message) { Add-Line $failure.Message }
		if ($failure.StackTrace) { Add-Line $failure.StackTrace }
		Add-Line '```'
		Add-Line '</details>'
		Add-Line

		if ($shown -le $MaxAnnotations)
		{
			# Point the annotation at the failing line so it lands on the PR diff.
			$location = ''
			if ($failure.StackTrace -match '\sin\s(?<file>.+):line\s(?<line>\d+)')
			{
				$path = $Matches.file
				$root = if ($env:GITHUB_WORKSPACE) { $env:GITHUB_WORKSPACE } else { (Get-Location).Path }
				if ($path.StartsWith($root, [StringComparison]::OrdinalIgnoreCase))
				{
					$relative = $path.Substring($root.Length).TrimStart('/', '\').Replace('\', '/')
					$location = "file=$relative,line=$($Matches.line),"
				}
			}
			# Annotations are single line, and '::' would be read as another command token.
			$message = ($failure.Message -replace '\s+', ' ').Replace('::', ':').Trim()
			Write-Output "::error $($location)title=$Title::$($failure.Name) [$($failure.Suite)]: $message"
		}
	}
}

}
catch
{
	Add-Line '> [!CAUTION]'
	Add-Line "> The test summary could not be generated: $($_.Exception.Message)"
	Write-Output "::error title=$Title::Test summary failed: $(($_.Exception.Message -replace '\s+', ' ').Replace('::', ':'))"
	throw
}
finally
{
	if ($env:GITHUB_STEP_SUMMARY)
	{
		Add-Content -LiteralPath $env:GITHUB_STEP_SUMMARY -Value $summary.ToString()
	}
	else
	{
		Write-Output $summary.ToString()
	}
}
