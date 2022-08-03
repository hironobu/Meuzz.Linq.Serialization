# Meuzz.Linq.Serialization

## 概要
Expression<T>型すなわち匿名関数を引数として受け取り、通信可能な形の文字列(またはバイト列)形式に変換するライブラリです。

## 備考
現時点では内部でJson.NETを使用しており、返還後の文字列はJson.NETで解読可能な文字列データとなっていますが、将来にわたってこの挙動を保証するものではありません。エンコーディング・シリアライズ処理の内容は今後の予告なく変更することがあります。