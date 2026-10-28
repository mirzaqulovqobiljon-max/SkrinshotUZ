"""Uzbek.isl tarjimasini o'rnatilgan Inno Setup'ning Default.isl fayli bilan solishtiradi.

Tekshiriladi: har bir xabar tarjima qilingan; %1, [name] kabi o'rinbosarlar va & (tezkor tugma) belgilari
asl matndagidek; noma'lum xabar nomlari yo'q. Ishlatish: python check-isl.py <Default.isl> <Uzbek.isl>
"""
import re
import sys

PLACEHOLDER = re.compile(r"%\d|\[\w+(?:/\w+)?\]")


def read(path):
    sections = {}
    current = None
    with open(path, encoding="utf-8-sig") as f:
        for line in f.read().splitlines():
            if line.startswith("["):
                current = line.strip()
                sections.setdefault(current, {})
            elif current and re.match(r"^[A-Za-z]\w*=", line):
                key, value = line.split("=", 1)
                sections[current][key] = value
    return sections


def main():
    english, uzbek = read(sys.argv[1]), read(sys.argv[2])
    errors = []
    for section in ("[Messages]", "[CustomMessages]"):
        en, uz = english.get(section, {}), uzbek.get(section, {})
        for key in sorted(set(en) - set(uz)):
            errors.append(f"{section} {key}: tarjima yo'q")
        for key in sorted(set(uz) - set(en)):
            errors.append(f"{section} {key}: bu Inno versiyasida bunday xabar yo'q")
        for key in sorted(set(en) & set(uz)):
            if sorted(PLACEHOLDER.findall(en[key])) != sorted(PLACEHOLDER.findall(uz[key])):
                errors.append(f"{section} {key}: o'rinbosarlar mos emas")
            if en[key].count("&") != uz[key].count("&"):
                errors.append(f"{section} {key}: '&' soni mos emas")
    for e in errors:
        print("XATO: " + e)
    total = len(uzbek.get("[Messages]", {})) + len(uzbek.get("[CustomMessages]", {}))
    print(f"Tekshirildi: {total} ta xabar, xatolar: {len(errors)}")
    return 1 if errors else 0


if __name__ == "__main__":
    sys.exit(main())
