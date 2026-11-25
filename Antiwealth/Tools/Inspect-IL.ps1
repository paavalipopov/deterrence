param([string]$TypeName, [string]$MethodName)
. (Join-Path $PSScriptRoot 'Inspect-Api.ps1')
$opcodeMap = @{}
[Reflection.Emit.OpCodes].GetFields([Reflection.BindingFlags]'Public,Static') | ForEach-Object {
    $opcode = $_.GetValue($null)
    $opcodeMap[[int]$opcode.Value -band 65535] = $opcode
}
$method = $assembly.GetType($TypeName, $true).GetMethods($flags) | Where-Object Name -eq $MethodName | Select-Object -First 1
$method.GetParameters() | ForEach-Object { "PARAM $($_.Position) $($_.Name) $($_.ParameterType)" }
$il = $method.GetMethodBody().GetILAsByteArray()
$position = 0
while ($position -lt $il.Length) {
    $offset = $position
    $code = [int]$il[$position++]
    if ($code -eq 254) { $code = 65280 + $il[$position++] }
    $opcode = $opcodeMap[$code]
    $operand = ''
    switch ($opcode.OperandType.ToString()) {
        'InlineNone' { }
        'ShortInlineVar' { $operand = $il[$position]; $position++ }
        'ShortInlineI' { $operand = [int]$il[$position]; $position++ }
        'ShortInlineBrTarget' { $jump = [int]$il[$position]; if ($jump -gt 127) { $jump -= 256 }; $operand = $position + 1 + $jump; $position++ }
        'InlineVar' { $operand = [BitConverter]::ToUInt16($il,$position); $position+=2 }
        'InlineI8' { $operand = [BitConverter]::ToInt64($il,$position); $position+=8 }
        'InlineR' { $operand = [BitConverter]::ToDouble($il,$position); $position+=8 }
        'ShortInlineR' { $operand = [BitConverter]::ToSingle($il,$position); $position+=4 }
        'InlineSwitch' { $count=[BitConverter]::ToInt32($il,$position); $position+=4+4*$count; $operand="switch $count" }
        default {
            $token = [BitConverter]::ToInt32($il,$position); $position+=4
            if ($opcode.OperandType.ToString() -eq 'InlineBrTarget') { $operand = $position+$token }
            elseif ($opcode.OperandType.ToString() -eq 'InlineI') { $operand=$token }
            elseif ($opcode.OperandType.ToString() -eq 'InlineString') { $operand=$method.Module.ResolveString($token) }
            else { try { $member=$method.Module.ResolveMember($token); $operand="$($member.DeclaringType).$member" } catch { $operand=$token } }
        }
    }
    '{0:D4}: {1} {2}' -f $offset,$opcode.Name,$operand
}
