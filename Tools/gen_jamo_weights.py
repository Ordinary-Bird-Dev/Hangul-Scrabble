#!/usr/bin/env python3
"""
Python port of Tools/gen_jamo_weights.ps1, with one difference: it reads
EVERY set in Assets/Resources rather than topik1_words.json alone.

That was already the intent -- TileDealer's own comment says the hand-set
ㅞ weight exists only because "ㅞ never occurs in topik1 but the
vocabB/vocabC sets need it", and calls the combined-corpus regen pending.

Method, identical to the PowerShell version:
  - decompose every word into cho / jung / jong
  - consonant demand counts BOTH cho and single jong, because one
    consonant tile serves either position; compound jong are never dealt
  - damp raw counts with x^0.7 so common jamo lead without flooding the
    tray, min 1 for anything that occurs at all, 0 if absent
"""

import json
import sys

CHO = [chr(c) for c in (0x3131, 0x3132, 0x3134, 0x3137, 0x3138, 0x3139, 0x3141,
                        0x3142, 0x3143, 0x3145, 0x3146, 0x3147, 0x3148, 0x3149,
                        0x314A, 0x314B, 0x314C, 0x314D, 0x314E)]
JUNG = [chr(c) for c in range(0x314F, 0x3164)]
JONG = [''] + [chr(c) for c in (0x3131, 0x3132, 0x3133, 0x3134, 0x3135, 0x3136,
                                0x3137, 0x3139, 0x313A, 0x313B, 0x313C, 0x313D,
                                0x313E, 0x313F, 0x3140, 0x3141, 0x3142, 0x3144,
                                0x3145, 0x3146, 0x3147, 0x3148, 0x314A, 0x314B,
                                0x314C, 0x314D, 0x314E)]


def tally(paths):
    cons = {k: 0 for k in CHO}
    vow = {k: 0 for k in JUNG}
    words = syllables = 0
    for p in paths:
        for e in json.load(open(p, encoding='utf-8'))['words']:
            words += 1
            for ch in e['word']:
                o = ord(ch)
                if not 0xAC00 <= o <= 0xD7A3:
                    continue
                off = o - 0xAC00
                syllables += 1
                cons[CHO[off // 588]] += 1
                vow[JUNG[(off % 588) // 28]] += 1
                f = JONG[off % 28]
                if f and f in cons:      # compound finals are not dealt
                    cons[f] += 1
    return cons, vow, words, syllables


def damp(n):
    return 0 if n == 0 else max(1, round(n ** 0.7))


def emit(name, order, counts):
    w = {k: damp(counts[k]) for k in order}
    lines = ['    private static readonly (string jamo, int weight)[] %s =' % name, '    {']
    for i in range(0, len(order), 5):
        chunk = order[i:i + 5]
        lines.append('        ' + ', '.join('("%s", %d)' % (k, w[k]) for k in chunk) + ',')
    lines[-1] = lines[-1].rstrip(',')
    lines.append('    };')
    return '\n'.join(lines), w


if __name__ == '__main__':
    cons, vow, words, syll = tally(sys.argv[1:])
    ctab, cw = emit('ChoseongWeights', CHO, cons)
    vtab, vw = emit('JungseongWeights', JUNG, vow)
    tc, tv = sum(cons.values()), sum(vow.values())
    print('// corpus: %d words, %d syllables' % (words, syll))
    print('// consonant tile demand %.1f%% of all tiles' % (100 * tc / (tc + tv)))
    print()
    print(ctab)
    print()
    print(vtab)
    print()
    we = chr(0x315E)
    print('// check  ㅞ corpus count = %d  weight = %d' % (vow[we], vw[we]))
    print('// zero-count jamo: %s' % ([k for k in CHO + JUNG
                                       if (cons.get(k, vow.get(k, 0)) or vow.get(k, 0)) == 0] or 'none'))
