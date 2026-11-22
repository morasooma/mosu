#include <stdint.h>
#include <stdlib.h>
#include <string.h>
#include <stdbool.h>

/* Function pointers are supplied from the exact SDL/BASS libraries loaded by
 * the host. No second SDL instance or dependency on system SDL is introduced. */
typedef struct {
    uint32_t mixer;
    bool (*set_device)(uint32_t);
    uint32_t (*get_data)(uint32_t, void *, uint32_t);
    bool (*put_data)(void *, const void *, int);
} audio_bridge;

void *mosu_audio_create(uint32_t mixer, void *set_device, void *get_data, void *put_data)
{
    audio_bridge *b = calloc(1, sizeof(*b));
    if (!b) return NULL;
    b->mixer = mixer;
    b->set_device = set_device;
    b->get_data = get_data;
    b->put_data = put_data;
    return b;
}

void mosu_audio_supply(void *userdata, void *stream, int additional, int total)
{
    (void)total;
    audio_bridge *b = userdata;
    if (!b || additional <= 0 || !b->set_device(0)) return;
    unsigned char data[1024];
    while (additional > 0) {
        int length = additional < (int)sizeof(data) ? additional : (int)sizeof(data);
        length = (length + 7) & ~7; /* whole float stereo frames */
        uint32_t read = b->get_data(b->mixer, data, (uint32_t)length);
        if (read == UINT32_MAX || read > (uint32_t)length) read = 0;
        if (read < (uint32_t)length) memset(data + read, 0, length - read);
        if (!b->put_data(stream, data, length)) break;
        additional -= length;
    }
}

/* Caller MUST detach the SDL callback (waiting for it) before freeing. */
void mosu_audio_destroy(void *userdata) { free(userdata); }
