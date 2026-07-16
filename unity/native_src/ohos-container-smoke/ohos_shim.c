// OHOS musl 扩展符号 shim（仅容器冒烟测试用）：
// - FORTIFY _chk 系列：签名取自 OHOS sysroot usr/include/fortify/*.h，转发到标准函数
// - __register_frame_info/__at_fini：OHOS musl 特有钩子，空实现
#include <stdio.h>
#include <string.h>
#include <stdarg.h>

size_t __fread_chk(void* buf, size_t size, size_t count, FILE* f, size_t bos) {
    (void)bos; return fread(buf, size, count, f);
}
size_t __fwrite_chk(const void* buf, size_t size, size_t count, FILE* f, size_t bos) {
    (void)bos; return fwrite(buf, size, count, f);
}
void* __memchr_chk(const void* s, int c, size_t n, size_t actual) {
    (void)actual; return memchr(s, c, n);
}
char* __strchr_chk(const char* p, int ch, size_t s_len) {
    (void)s_len; return strchr(p, ch);
}
size_t __strlen_chk(const char* s, size_t s_len) {
    (void)s_len; return strlen(s);
}
void* __memcpy_chk(void* dst, const void* src, size_t len, size_t dstlen) {
    (void)dstlen; return memcpy(dst, src, len);
}
int __vsnprintf_chk(char* s, size_t maxlen, int flag, size_t slen, const char* fmt, va_list ap) {
    (void)flag; (void)slen; return vsnprintf(s, maxlen, fmt, ap);
}
void __register_frame_info(const void* begin, void* ob) { (void)begin; (void)ob; }
void* __deregister_frame_info(const void* begin) { (void)begin; return 0; }
void __at_fini(void* func) { (void)func; }
