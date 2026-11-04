# Credits and third-party licences

## Vocabulary

The word lists in `Assets/Resources/topik1_words.json`, `vocabB_words.json`
and `vocabC_words.json` are derived from **한국어기초사전 (Korean Basic
Dictionary)**, published by the **National Institute of Korean Language
(국립국어원)**, at <https://krdict.korean.go.kr/>.

Used under **Creative Commons Attribution–ShareAlike 2.0 Korea**
(<https://creativecommons.org/licenses/by-sa/2.0/kr/>).

Headwords, English glosses and most example sentences come from that
dictionary. Some example sentences in the beginner set are the project's
own, carried over from the previous word lists; the co-owner's import
spreadsheets record the origin of every sentence in an `example_source`
column.

### Obligations this places on the project

The licence is Attribution–ShareAlike, so:

1. **Attribution must be visible to players**, not only recorded here.
   `InfoPageController.VocabCredit` holds the credit string and writes it
   into a `VocabCreditText` label on the How to Play or Hangul Basics
   page. If that label is missing the controller logs a warning — do not
   ship a build that logs it.

2. **ShareAlike applies to the derived material.** The licence requires a
   "work product" built from the material to be distributed under the same
   licence, and it does not define how far that reaches into a game that
   bundles the data. This has not been resolved. Before any commercial
   release, or any release where the project's own licensing matters,
   get a view on whether ShareAlike attaches to the JSON files alone or to
   the game as a whole.

3. The English and Korean versions of the policy differ slightly in scope:
   the Korean text covers materials "not separately specified"
   (별도로 명시되지 않은 모든 자료), the English text says all materials.

Policy pages, for reference:

- English: <https://krdict.korean.go.kr/eng/kboardPolicy/copyRightTermsInfo>
- Korean: <https://krdict.korean.go.kr/kor/kboardPolicy/copyRightTermsInfo>

## Audio

Sound effects in `Assets/Resources/` are named by their Freesound IDs and
contributors. Their individual licences have not been audited — see the
outstanding task list.
