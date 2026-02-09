# Rule: unused-code

Dead code detection checks. These reduce confusion, maintenance burden, and compilation noise.

## Rules

### unused-code/unused-usings 🔵 LOW
**Unused `using` directives.** Flag but low priority. IDEs handle these automatically.

### unused-code/unused-private-members 🟡 MEDIUM
**Unused private methods, fields, properties.** These add confusion and maintenance burden. If a private member has no references within its containing class, flag it.

Exception: Members used via reflection (e.g., `[JsonProperty]`, serialization). Look for serialization attributes before flagging.

### unused-code/unused-parameters 🟡 MEDIUM
**Unused parameters in methods.** Check if they're part of an interface contract (acceptable — the interface requires the parameter even if this implementation ignores it) or truly dead (flag for removal).

```csharp
// ✅ OK — interface requires the parameter
public void Handle(Event e, CancellationToken ct) { /* ct unused but interface demands it */ }

// ❌ Not an interface method, parameter is dead weight
private void ProcessData(string data, bool verbose) { Console.WriteLine(data); /* verbose unused */ }
```

### unused-code/dead-branches 🟡 MEDIUM
**Dead branches:** `if (false)`, `#if` directives for removed features, unreachable code after `return`/`throw`. Flag for cleanup.

### unused-code/commented-out-code 🟡 MEDIUM
**Commented-out code blocks.** Flag for removal. Use source control for history, not comments.

Small explanatory comments are fine. Flag blocks of 3+ commented-out lines that look like executable code.

### unused-code/unused-registrations 🟡 MEDIUM
**Unused Autofac registrations.** Services registered in the DI container but never resolved anywhere. These add startup cost and confusion.

### unused-code/empty-catch 🟠 HIGH
**Empty catch blocks.** `catch (Exception) { }` or `catch { }` is almost always a bug. At minimum, log the exception. The only acceptable case is a deliberate decision to suppress a specific, documented exception type.

```csharp
// ❌ Silently swallowed
try { Save(); } catch { }

// ✅ At minimum, log
try { Save(); }
catch (Exception ex) { _logger.LogWarning(ex, "Save failed, continuing"); }

// ✅ Specific, documented suppression is acceptable
try { File.Delete(tempPath); }
catch (IOException) { /* Best-effort cleanup — file will be cleaned by next restart */ }
```

### unused-code/unused-events 🟡 MEDIUM
**Unused event subscriptions.** Subscribing to an event/observable but never using the result or never triggering the handler path.
