# Copyright 2026 Stephan Schlöpke
# SPDX-License-Identifier: Apache-2.0
# TextMate supplies a zero-based UTF-8 byte offset in TM_LINE_INDEX.
use strict;
use warnings;
use JSON::PP;
use FindBin;
exit if ($ENV{TM_SCOPE} // '') =~ /(?:^|\s)(?:comment|string)\./;
open my $fh, '<:raw', "$FindBin::Bin/completions.json" or die $!;
my $data = decode_json(do { local $/; <$fh> });
my $line = $ENV{TM_CURRENT_LINE} // '';
my $before = substr($line, 0, $ENV{TM_LINE_INDEX} // length($line));
my $words = $data->{keywords};
my $add = '';
for my $context (@{$data->{contexts}}) {
    my $pattern = $context->{pattern};
    if ($before =~ /$pattern/) {
        $add = ':' if $context->{typePrefix} && !$1;
        $words = $data->{$context->{group}};
        last;
    }
}
my $prefix = $ENV{TM_CURRENT_WORD} // '';
# TextMate replaces the current word, not its preceding punctuation.
$add = ':' if $prefix =~ /^:/ && $words == $data->{types};
$prefix =~ s/^://;
for my $word (@$words) {
    print "$add$word\n" if index(lc($word), lc($prefix)) == 0;
}
