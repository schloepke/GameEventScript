# Copyright 2026 Stephan Schlöpke
# SPDX-License-Identifier: Apache-2.0
use strict;
use warnings;
use File::Spec;
use File::Basename qw(dirname);
$| = 1;
sub html { my $s = shift; $s =~ s/&/&amp;/g; $s =~ s/</&lt;/g; $s =~ s/>/&gt;/g; $s =~ s/"/&quot;/g; return $s; }
sub uri { my $s = shift; $s =~ s/([^A-Za-z0-9\-._~])/sprintf('%%%02X',ord($1))/ge; return $s; }
print '<!doctype html><meta charset="utf-8"><style>body{font-family:monospace}pre{white-space:pre-wrap}</style><pre>';
while (my $line = <STDIN>) {
    if ($line =~ /^(.*)\((\d+),(\d+)\): (.*)$/) {
        my ($file,$row,$column,$rest) = ($1,$2,$3,$4);
        my $label = "$file($row,$column)";
        $file = File::Spec->rel2abs($file, dirname($ENV{TM_FILEPATH} // '/'));
        my $path = uri($file); $path =~ s/%2F/\//g;
        my $url = 'txmt://open?url=' . uri('file://' . $path) . "&line=$row&column=$column";
        print '<a href="',html($url),'">',html($label),'</a>: ',html($rest),"\n";
    } else { print html($line); }
}
print '</pre>';
