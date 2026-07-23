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

#[cfg(test)]
mod tests {
    use super::{deserialize_non_blank, deserialize_revision};
    use serde::Deserialize;

    #[derive(Deserialize)]
    struct NonBlankFixture {
        #[serde(deserialize_with = "deserialize_non_blank")]
        value: String,
    }

    #[derive(Deserialize)]
    struct RevisionFixture {
        #[serde(deserialize_with = "deserialize_revision")]
        value: String,
    }

    #[test]
    fn validation_helpers_preserve_valid_values() {
        let nonblank: NonBlankFixture =
            serde_json::from_str(r#"{"value":" value "}"#).expect("the value must deserialize");
        let revision: RevisionFixture =
            serde_json::from_str(r#"{"value":"0123456789abcdef0123456789abcdef01234567"}"#)
                .expect("the revision must deserialize");

        assert_eq!(nonblank.value, " value ");
        assert_eq!(revision.value, "0123456789abcdef0123456789abcdef01234567");
    }
}
