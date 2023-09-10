from ThaiPersonalCardExtract import PersonalCard


def extract_thai_id_info(path):
    reader = PersonalCard(lang="mix")
    result = reader.extract_front_info(path)
 
    return result
