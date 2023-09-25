from ThaiPersonalCardExtract import PersonalCard


def card_to_dict(card):
    return {
        'Identification_Number': card.Identification_Number,
        'FullNameTH': card.FullNameTH,
        'PrefixTH': card.PrefixTH,
        'NameTH': card.NameTH,
        'LastNameTH': card.LastNameTH,
        'PrefixEN': card.PrefixEN,
        'NameEN': card.NameEN,
        'LastNameEN': card.LastNameEN,
        'BirthdayTH': card.BirthdayTH,
        'BirthdayEN': card.BirthdayEN,
        'Religion': card.Religion,
        'Address': card.Address,
        'DateOfIssueTH': card.DateOfIssueTH,
        'DateOfIssueEN': card.DateOfIssueEN,
        'DateOfExpiryTH': card.DateOfExpiryTH,
        'DateOfExpiryEN': card.DateOfExpiryEN,
        'LaserCode': card.LaserCode
    }


def extract_thai_id_front_info(path):
    reader = PersonalCard(lang="mix")
    result = reader.extract_front_info(path)
    print(f'Thai ID front info: {result}')
    return result


def extract_thai_id_back_info(path):
    reader = PersonalCard(lang="mix")
    result = reader.extract_back_info(path)
    print(f'Thai ID back info: {result}')
    return result
