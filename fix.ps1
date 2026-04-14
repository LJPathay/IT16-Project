$files = Get-ChildItem -Path "c:\Users\lebro\source\repos\ljp_itsolutions\ljp_itsolutions\Views" -Filter "*.cshtml" -Recurse
foreach ($file in $files) {
    $content = Get-Content $file.FullName -Raw
    
    # 1. Fix "Add onKeyPress|onKeyDown|onKeyUp"
    # Adds keyboard support to elements with onclick
    $content = [regex]::Replace($content, '(?i)<(div|span|i|a|li|tr)([^>]*\bonclick=["''][^"'']+["''][^>]*)>', {
        param($m)
        $tag = $m.Groups[1].Value
        $attrs = $m.Groups[2].Value
        
        $newAttrs = $attrs
        if ($newAttrs -notmatch '\bonkey(press|down|up)=') { 
            # Inject onkeypress at the end of attrs safely
            $newAttrs += ' onkeypress="if(event.key === ''Enter'') this.click()"'
        }
        
        return "<$tag$newAttrs>"
    })

    # 2. Add empty aria-label to forms again but matching broader rules just in case
    # some forms were missed (like input type=password). Note we avoid touching hidden/submit.
    $content = [regex]::Replace($content, '(?i)<(input|select|textarea)([^>]*)>', {
        param($m)
        $tag = $m.Groups[1].Value
        $attrs = $m.Groups[2].Value
        if ($attrs -match 'type=["'']?(hidden|submit|button)["'']?') { return $m.Value }
        
        $newAttrs = $attrs
        if ($newAttrs -notmatch '\baria-label=' -and $newAttrs -notmatch '\bid=' -and $newAttrs -notmatch 'aria-labelledby') { 
            $name = "Input field"
            if ($attrs -match 'name=["'']([^"'']+)["'']') { $name = $matches[1] }
            $newAttrs += " aria-label=`"$name`""
        }
        
        return "<$tag$newAttrs>"
    })

    # 3. Add Alt to img tags missing alt
    $content = [regex]::Replace($content, '(?i)<img(?![^>]*\balt=)([^>]*)>', '<img alt="Image"$1>')

    Set-Content $file.FullName $content -NoNewline
}
