// Comparação de versões "1.2.3" (as do manifest do Chrome e a do próprio atualizador).

use std::cmp::Ordering;

fn partes(v: &str) -> Vec<u64> {
    v.trim()
        .trim_start_matches(['v', 'V'])
        .split('.')
        .map(|p| p.chars().take_while(char::is_ascii_digit).collect::<String>().parse().unwrap_or(0))
        .collect()
}

pub fn comparar(a: &str, b: &str) -> Ordering {
    let (x, y) = (partes(a), partes(b));
    for i in 0..x.len().max(y.len()) {
        let o = x.get(i).copied().unwrap_or(0).cmp(&y.get(i).copied().unwrap_or(0));
        if o != Ordering::Equal {
            return o;
        }
    }
    Ordering::Equal
}

/// `a` é mais nova que `b`? Nunca se rebaixa: versão igual ou menor não é atualização.
pub fn mais_nova(a: &str, b: &str) -> bool {
    comparar(a, b) == Ordering::Greater
}

#[cfg(test)]
mod testes {
    use super::*;

    #[test]
    fn patch_cresce_sem_rolar() {
        assert!(mais_nova("0.5.10", "0.5.9"));
        assert!(mais_nova("0.5.27", "0.5.26"));
        assert!(!mais_nova("0.5.9", "0.5.10"));
    }

    #[test]
    fn igual_ou_menor_nao_atualiza() {
        assert!(!mais_nova("0.5.26", "0.5.26"));
        assert!(!mais_nova("0.4.0", "0.9.0"));
        assert!(!mais_nova("1.0", "1.0.0"));
    }

    #[test]
    fn tamanhos_diferentes_e_prefixo() {
        assert!(mais_nova("1.0.0.1", "1.0.0"));
        assert!(mais_nova("v1.2.0", "1.1.9"));
        assert!(mais_nova("1", "0.9.9"));
    }

    #[test]
    fn lixo_vale_zero() {
        assert!(!mais_nova("", "0.0.1"));
        assert!(mais_nova("0.0.1", "abc"));
    }
}
