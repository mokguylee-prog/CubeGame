# AI 연동 디버깅 기록

작성일: 2026-05-25

---

## 문제 1: `KeyNotFoundException` — API 에러 응답 파싱 실패

### 증상
AI 풀기 버튼 클릭 시:
```
The given key was not present in the dictionary
```

### 원인
OpenRouter API가 오류 시 반환하는 JSON 구조:
```json
{"error": {"message": "...", "code": 404}}
```
기존 코드가 `root.GetProperty("choices")` 를 무조건 호출 → `"choices"` 키가 없으면 `KeyNotFoundException` 발생.

### 수정 (`AiSolverService.cs` → `CallAsync`)
```csharp
// 수정 전 (예외 발생)
var choices = root.GetProperty("choices");

// 수정 후 (안전하게 처리)
if (root.TryGetProperty("error", out var errEl))
{
    var msg  = errEl.TryGetProperty("message", out var m) ? m.GetString() : null;
    int code = 0;
    if (errEl.TryGetProperty("code", out var c))
        code = c.ValueKind == JsonValueKind.Number ? c.GetInt32()
             : int.TryParse(c.GetString(), out var n) ? n : 0;
    return $"❌ API 오류 [{code}]: {msg ?? "알 수 없는 오류"}";
}
if (!root.TryGetProperty("choices", out var choices) || choices.GetArrayLength() == 0)
    return $"⚠️ 응답 형식 오류\n{json[..Math.Min(json.Length, 80)]}";
```

---

## 문제 2: 404 — Free 모델 폐기

### 증상
```json
{"error":{"message":"No endpoints found for google/gemma-3-27b-it:free.","code":404}}
```

### 원인
기존 모델 3종이 OpenRouter에서 제거됨:
- `google/gemma-3-27b-it:free` ❌ 폐기
- `meta-llama/llama-3.2-11b-vision-instruct:free` ❌ 폐기
- `mistralai/mistral-7b-instruct:free` ❌ 폐기

### 수정
`FreeModels` 배열을 2026-05-25 기준 동작 확인된 모델로 교체:
```csharp
private static readonly string[] FreeModels =
[
    "deepseek/deepseek-v4-flash:free",
    "openai/gpt-oss-20b:free",
    "google/gemma-4-31b-it:free",
    "meta-llama/llama-3.3-70b-instruct:free",
    "meta-llama/llama-3.2-3b-instruct:free",
];
```

---

## 문제 3: 429 — Free 모델 Rate Limit

### 증상
```json
{"error":{"message":"Provider returned error","code":429,"metadata":{"raw":"... is temporarily rate-limited upstream ..."}}}
```

### 원인
Free 모델은 공유 rate limit이 매우 낮아 단일 모델 호출 시 자주 실패.

### 수정 — 모델 순차 Fallback
`CallAsync`를 `FreeModels` 전체를 순회하도록 변경:
- 404 (모델 없음) → 다음 모델로 fallback
- 429 (rate limit) → 다음 모델로 fallback
- 그 외 오류 → 즉시 사용자에게 반환
- 모든 모델 실패 시 → 마지막 에러 메시지 반환

```csharp
foreach (var model in FreeModels)
{
    // ... HTTP 호출 ...
    if (code == 404 || code == 429) { lastError = ...; continue; }
    // 성공 시 return
}
return lastError ?? "❌ 사용 가능한 AI 모델이 없습니다.";
```

---

## 문제 4: AI 모드가 텍스트 조언만 하고 큐브를 실제로 움직이지 않음

### 증상
AI 풀기 버튼 클릭 → 응답 패널에 한국어 설명만 나타남. 큐브는 정지.

### 원인
`SolveController.StartAiOnlyMode()` 가 `onComplete: _ => { IsRunning = false; }` 로
물리 이동 없이 종료. `AiSolverService.GetAiSolutionAsync()` 도 텍스트 조언만 반환.

### 수정 — SubAgent 2단계 파이프라인 구현

**아키텍처 (새 파일/클래스):**

```
AI 풀기 클릭 (UseHistoryMode=false)
    │
    ├─► [Agent 1] AiSolverService.GetCubeAnalysisAsync()
    │       영어 프롬프트로 큐브 상태 분석 (2-3문장)
    │
    ├─► [Agent 2] AiSolverService.GetSolvingMovesAsync()
    │       system 프롬프트: "표기법 토큰만 출력"
    │       user 프롬프트: 분석결과 + 큐브상태
    │       → 예: "R U R' U' R' F R2 U' R' U' R U R' F'"
    │
    ├─► [C# 검증] CubeVerifier.VerifySolution()
    │       Cube3x3 딥카피 → 이동 시뮬레이션 → IsSolved() 확인
    │       ✅ 검증 성공 → Queue<(LayerAxis,int,bool)> → 큐 실행
    │       ❌ 검증 실패 → 힌트 강화 후 Agent2 재시도 (최대 3회)
    │
    └─► 3회 모두 실패 → "기록 역재생 체크박스 ON 권장" 안내
```

**새 파일:**
- `AI/RubikNotation.cs` — 표준 표기법 파서 (U/D/R/L/F/B + '/2, M/E/S)
- `AI/CubeVerifier.cs` — Cube3x3 딥카피 + IsSolved() + VerifySolution()

**수정된 파일:**
- `AI/AiSolverService.cs` — `GetCubeAnalysisAsync()`, `GetSolvingMovesAsync()`, `CallWithSystemAsync()` 추가
- `AI/SolveController.cs` — `StartAiPipelineMode()`, `RunAiPipelineAsync()` 추가
- `MainWindow.cs` — `AiSolve()` 에서 `_cube` 참조 전달

**참고:** Free LLM은 복잡한 큐브를 완전히 풀지 못할 수 있음.
  → 검증 실패 시 "기록 역재생 모드(체크박스 ON)" 권장 메시지 표시

---

## 참고: Free 모델 업데이트 방법

OpenRouter free 모델 목록 확인:
```bash
curl -s "https://openrouter.ai/api/v1/models" \
  -H "Authorization: Bearer $OPENROUTER_API_KEY" | \
  python3 -c "
import json, sys
data = json.load(sys.stdin)
free = [m for m in data.get('data', []) if ':free' in m.get('id','')]
for m in free: print(m['id'])
"
```

특정 모델 동작 확인:
```bash
curl -s -X POST "https://openrouter.ai/api/v1/chat/completions" \
  -H "Authorization: Bearer $OPENROUTER_API_KEY" \
  -H "Content-Type: application/json" \
  -d '{"model":"deepseek/deepseek-v4-flash:free","max_tokens":20,"messages":[{"role":"user","content":"hi"}]}'
```
