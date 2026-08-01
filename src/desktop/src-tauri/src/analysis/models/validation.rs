use serde::Deserialize;
use serde::de::Error;

pub(super) fn deserialize_non_blank<'de, D>(deserializer: D) -> Result<String, D::Error>
where
    D: serde::Deserializer<'de>,
{
    let value = String::deserialize(deserializer)?;

    if value.trim().is_empty() {
        return Err(D::Error::custom("the value must not be blank"));
    }

    Ok(value)
}

pub(super) fn deserialize_revision<'de, D>(deserializer: D) -> Result<String, D::Error>
where
    D: serde::Deserializer<'de>,
{
    let value = String::deserialize(deserializer)?;
    let bytes = value.as_bytes();
    let has_valid_length = matches!(bytes.len(), 40 | 64);
    let is_lowercase_hex = bytes
        .iter()
        .all(|byte| matches!(byte, b'0'..=b'9' | b'a'..=b'f'));

    if !has_valid_length || !is_lowercase_hex {
        return Err(D::Error::custom(
            "the revision must be a full lowercase hexadecimal object identifier",
        ));
    }

    Ok(value)
}

pub(super) fn deserialize_token<'de, D>(deserializer: D) -> Result<String, D::Error>
where
    D: serde::Deserializer<'de>,
{
    let value = String::deserialize(deserializer)?;
    let bytes = value.as_bytes();

    if bytes.len() != 64
        || !bytes
            .iter()
            .all(|byte| matches!(byte, b'0'..=b'9' | b'a'..=b'f'))
    {
        return Err(D::Error::custom(
            "the token must be a lowercase SHA-256 hexadecimal value",
        ));
    }

    Ok(value)
}

pub(super) fn deserialize_run_id<'de, D>(deserializer: D) -> Result<String, D::Error>
where
    D: serde::Deserializer<'de>,
{
    let value = String::deserialize(deserializer)?;
    let bytes = value.as_bytes();
    let is_hyphenated =
        bytes.len() == 36 && [8, 13, 18, 23].iter().all(|index| bytes[*index] == b'-');
    let is_lowercase_hex = bytes.iter().enumerate().all(|(index, byte)| {
        [8, 13, 18, 23].contains(&index) || matches!(byte, b'0'..=b'9' | b'a'..=b'f')
    });
    let is_version_four = bytes.get(14) == Some(&b'4');
    let has_valid_variant = matches!(bytes.get(19), Some(b'8' | b'9' | b'a' | b'b'));

    if !is_hyphenated || !is_lowercase_hex || !is_version_four || !has_valid_variant {
        return Err(D::Error::custom(
            "the run id must be a canonical lowercase version-4 UUID",
        ));
    }

    Ok(value)
}

pub(super) fn deserialize_repository_id<'de, D>(deserializer: D) -> Result<String, D::Error>
where
    D: serde::Deserializer<'de>,
{
    let value = String::deserialize(deserializer)?;
    let bytes = value.as_bytes();
    let valid = bytes.len() == 36
        && [8, 13, 18, 23].iter().all(|index| bytes[*index] == b'-')
        && bytes.iter().enumerate().all(|(index, byte)| {
            [8, 13, 18, 23].contains(&index) || matches!(byte, b'0'..=b'9' | b'a'..=b'f')
        });

    if !valid {
        return Err(D::Error::custom(
            "repository identifiers must be lowercase hyphenated GUIDs",
        ));
    }

    Ok(value)
}

pub(super) fn deserialize_ref_name<'de, D>(deserializer: D) -> Result<String, D::Error>
where
    D: serde::Deserializer<'de>,
{
    let value = String::deserialize(deserializer)?;
    let suffix = value
        .strip_prefix("refs/heads/")
        .or_else(|| value.strip_prefix("refs/remotes/"));

    if suffix.is_none_or(|name| name.trim().is_empty())
        || value.contains('\0')
        || value.chars().count() > 4_096
    {
        return Err(D::Error::custom(
            "the ref must be a nonblank local or remote-tracking full reference",
        ));
    }

    Ok(value)
}

pub(super) fn deserialize_failure_code<'de, D>(deserializer: D) -> Result<String, D::Error>
where
    D: serde::Deserializer<'de>,
{
    let value = String::deserialize(deserializer)?;

    if value.trim().is_empty() || value.chars().count() > 128 {
        return Err(D::Error::custom(
            "the failure code must be a nonblank value of at most 128 characters",
        ));
    }

    Ok(value)
}
