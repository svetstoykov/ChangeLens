use serde::Deserialize;
use serde::de::Error;

const MAX_REF_SCALARS: usize = 4_096;
const MAX_I32_COUNT: u64 = i32::MAX as u64;

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
    let is_valid_length = matches!(bytes.len(), 40 | 64);
    let is_lowercase_hex = bytes
        .iter()
        .all(|byte| matches!(byte, b'0'..=b'9' | b'a'..=b'f'));

    if !is_valid_length || !is_lowercase_hex {
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

pub(super) fn deserialize_ref_name<'de, D>(deserializer: D) -> Result<String, D::Error>
where
    D: serde::Deserializer<'de>,
{
    let value = String::deserialize(deserializer)?;

    validate_ref_name(&value).map_err(D::Error::custom)?;

    Ok(value)
}

pub(super) fn validate_ref_name(value: &str) -> Result<(), &'static str> {
    let suffix = value
        .strip_prefix("refs/heads/")
        .or_else(|| value.strip_prefix("refs/remotes/"));

    if suffix.is_none_or(|name| name.trim().is_empty())
        || value.contains('\0')
        || value.chars().count() > MAX_REF_SCALARS
    {
        return Err("the ref must be a nonblank local or remote-tracking full reference");
    }

    Ok(())
}

pub(super) fn deserialize_count<'de, D>(deserializer: D) -> Result<u32, D::Error>
where
    D: serde::Deserializer<'de>,
{
    let value = u64::deserialize(deserializer)?;

    validate_count(value).map_err(D::Error::custom)
}

pub(super) fn validate_count(value: u64) -> Result<u32, &'static str> {
    if value > MAX_I32_COUNT {
        return Err("the count exceeds the protocol maximum");
    }

    Ok(value as u32)
}
