// SPDX-License-Identifier: GPL-2.0-or-later
/*
 * cxadc-win - CX2388x ADC DMA driver for Windows
 *
 * Copyright (C) 2024 Jitterbug
 *
 * Based on the Linux version created by
 * Copyright (C) 2005-2007 Hew How Chee <how_chee@yahoo.com>
 * Copyright (C) 2013-2015 Chad Page <Chad.Page@gmail.com>
 * Copyright (C) 2019-2023 Adam Sampson <ats@offog.org>
 * Copyright (C) 2020-2022 Tony Anderson <tandersn@cs.washington.edu>
 */

#include "precomp.h"
#include "ioctl.tmh"

#include "ioctl.h"
#include "cx2388x.h"

VOID cx_evt_io_ctrl(
    _In_ WDFQUEUE queue,
    _In_ WDFREQUEST req,
    _In_ size_t out_len,
    _In_ size_t in_len,
    _In_ ULONG ctrl_code)
{
    NTSTATUS status = STATUS_SUCCESS;
    PLONG out_buf, in_buf;
    PDEVICE_CONTEXT dev_ctx = cx_device_get_ctx(WdfIoQueueGetDevice(queue));

    switch (ctrl_code)
    {
    case CX_IOCTL_GET_CAPTURE_STATE:
        status = WdfRequestRetrieveOutputBuffer(req, out_len, &out_buf, NULL);

        if (!NT_SUCCESS(status))
        {
            TraceEvents(TRACE_LEVEL_ERROR, DBG_GENERAL, "WdfRequestRetrieveOutputBuffer (get_capture_state) failed with status %!STATUS!", status);
            break;
        }

        *out_buf = (ULONG)dev_ctx->state.is_capturing;
        WdfRequestSetInformation(req, (ULONG_PTR)sizeof(*out_buf));
        break;

    case CX_IOCTL_GET_OUFLOW_COUNT:
        status = WdfRequestRetrieveOutputBuffer(req, out_len, &out_buf, NULL);

        if (!NT_SUCCESS(status))
        {
            TraceEvents(TRACE_LEVEL_ERROR, DBG_GENERAL, "WdfRequestRetrieveOutputBuffer (over/underflow count) failed with status %!STATUS!", status);
            break;
        }

        *out_buf = dev_ctx->state.ouflow_count;
        WdfRequestSetInformation(req, (ULONG_PTR)sizeof(*out_buf));
        break;

    case CX_IOCTL_GET_VMUX:
        status = WdfRequestRetrieveOutputBuffer(req, out_len, &out_buf, NULL);

        if (!NT_SUCCESS(status))
        {
            TraceEvents(TRACE_LEVEL_ERROR, DBG_GENERAL, "WdfRequestRetrieveOutputBuffer (vmux) failed with status %!STATUS!", status);
            break;
        }

        *out_buf = dev_ctx->attrs.vmux;
        WdfRequestSetInformation(req, (ULONG_PTR)sizeof(*out_buf));
        break;

    case CX_IOCTL_GET_LEVEL:
        status = WdfRequestRetrieveOutputBuffer(req, out_len, &out_buf, NULL);

        if (!NT_SUCCESS(status))
        {
            TraceEvents(TRACE_LEVEL_ERROR, DBG_GENERAL, "WdfRequestRetrieveOutputBuffer (level) failed with status %!STATUS!", status);
            break;
        }

        *out_buf = dev_ctx->attrs.level;
        WdfRequestSetInformation(req, (ULONG_PTR)sizeof(*out_buf));
        break;

    case CX_IOCTL_GET_TENBIT:
        status = WdfRequestRetrieveOutputBuffer(req, out_len, &out_buf, NULL);

        if (!NT_SUCCESS(status))
        {
            TraceEvents(TRACE_LEVEL_ERROR, DBG_GENERAL, "WdfRequestRetrieveOutputBuffer (tenbit) failed with status %!STATUS!", status);
            break;
        }

        *out_buf = dev_ctx->attrs.tenbit;
        WdfRequestSetInformation(req, (ULONG_PTR)sizeof(*out_buf));
        break;

    case CX_IOCTL_GET_SIXDB:
        status = WdfRequestRetrieveOutputBuffer(req, out_len, &out_buf, NULL);

        if (!NT_SUCCESS(status))
        {
            TraceEvents(TRACE_LEVEL_ERROR, DBG_GENERAL, "WdfRequestRetrieveOutputBuffer (sixdb) failed with status %!STATUS!", status);
            break;
        }

        *out_buf = dev_ctx->attrs.sixdb;
        WdfRequestSetInformation(req, (ULONG_PTR)sizeof(*out_buf));
        break;

    case CX_IOCTL_GET_CENTER_OFFSET:
        status = WdfRequestRetrieveOutputBuffer(req, out_len, &out_buf, NULL);

        if (!NT_SUCCESS(status))
        {
            TraceEvents(TRACE_LEVEL_ERROR, DBG_GENERAL, "WdfRequestRetrieveOutputBuffer (center_offset) failed with status %!STATUS!", status);
            break;
        }

        *out_buf = dev_ctx->attrs.center_offset;
        WdfRequestSetInformation(req, (ULONG_PTR)sizeof(*out_buf));
        break;

    case CX_IOCTL_RESET_OUFLOW_COUNT:
        TraceEvents(TRACE_LEVEL_INFORMATION, DBG_GENERAL, "resetting over/underflow count (current: %d)", dev_ctx->state.ouflow_count);
        dev_ctx->state.ouflow_count = 0;
        break;

    case CX_IOCTL_SET_VMUX:
        status = WdfRequestRetrieveInputBuffer(req, in_len, &in_buf, NULL);

        if (!NT_SUCCESS(status))
        {
            TraceEvents(TRACE_LEVEL_ERROR, DBG_GENERAL, "WdfRequestRetrieveInputBuffer (vmux) failed with status %!STATUS!", status);
            break;
        }

        if (*in_buf < CX_IOCTL_VMUX_MIN || *in_buf > CX_IOCTL_VMUX_MAX)
        {
            TraceEvents(TRACE_LEVEL_ERROR, DBG_GENERAL, "invalid vmux %d", *in_buf);
            status = STATUS_INVALID_PARAMETER;
            break;
        }

        TraceEvents(TRACE_LEVEL_INFORMATION, DBG_GENERAL, "setting vmux to %d", *in_buf);
        dev_ctx->attrs.vmux = *in_buf;
        cx_set_vmux(dev_ctx);
        break;

    case CX_IOCTL_SET_LEVEL:
        status = WdfRequestRetrieveInputBuffer(req, in_len, &in_buf, NULL);

        if (!NT_SUCCESS(status))
        {
            TraceEvents(TRACE_LEVEL_ERROR, DBG_GENERAL, "WdfRequestRetrieveInputBuffer (level) failed with status %!STATUS!", status);
            break;
        }

        if (*in_buf < CX_IOCTL_LEVEL_MIN || *in_buf > CX_IOCTL_LEVEL_MAX)
        {
            TraceEvents(TRACE_LEVEL_ERROR, DBG_GENERAL, "invalid level %d", *in_buf);
            status = STATUS_INVALID_PARAMETER;
            break;
        }

        TraceEvents(TRACE_LEVEL_INFORMATION, DBG_GENERAL, "setting level to %d", *in_buf);
        dev_ctx->attrs.level = *in_buf;
        cx_set_level(dev_ctx);
        break;

    case CX_IOCTL_SET_TENBIT:
        status = WdfRequestRetrieveInputBuffer(req, in_len, &in_buf, NULL);

        if (!NT_SUCCESS(status))
        {
            TraceEvents(TRACE_LEVEL_ERROR, DBG_GENERAL, "WdfRequestRetrieveInputBuffer (tenbit) failed with status %!STATUS!", status);
            break;
        }

        if (*in_buf < CX_IOCTL_TENBIT_MIN || *in_buf > CX_IOCTL_TENBIT_MAX)
        {
            TraceEvents(TRACE_LEVEL_ERROR, DBG_GENERAL, "invalid tenbit %d", *in_buf);
            status = STATUS_INVALID_PARAMETER;
            break;
        }

        TraceEvents(TRACE_LEVEL_INFORMATION, DBG_GENERAL, "setting tenbit to %d", *in_buf);
        dev_ctx->attrs.tenbit = *in_buf;
        cx_set_tenbit(dev_ctx);
        break;

    case CX_IOCTL_SET_SIXDB:
        status = WdfRequestRetrieveInputBuffer(req, in_len, &in_buf, NULL);

        if (!NT_SUCCESS(status))
        {
            TraceEvents(TRACE_LEVEL_ERROR, DBG_GENERAL, "WdfRequestRetrieveInputBuffer (sixdb) failed with status %!STATUS!", status);
            break;
        }

        if (*in_buf < CX_IOCTL_SIXDB_MIN || *in_buf > CX_IOCTL_SIXDB_MAX)
        {
            TraceEvents(TRACE_LEVEL_ERROR, DBG_GENERAL, "invalid sixdb %d", *in_buf);
            status = STATUS_INVALID_PARAMETER;
            break;
        }

        TraceEvents(TRACE_LEVEL_INFORMATION, DBG_GENERAL, "setting sixdb to %d", *in_buf);
        dev_ctx->attrs.sixdb = *in_buf;
        cx_set_level(dev_ctx);
        break;

    case CX_IOCTL_SET_CENTER_OFFSET:
        status = WdfRequestRetrieveInputBuffer(req, in_len, &in_buf, NULL);

        if (!NT_SUCCESS(status))
        {
            TraceEvents(TRACE_LEVEL_ERROR, DBG_GENERAL, "WdfRequestRetrieveInputBuffer (center_center) failed with status %!STATUS!", status);
            break;
        }

        if (*in_buf < CX_IOCTL_CENTER_OFFSET_MIN || *in_buf > CX_IOCTL_CENTER_OFFSET_MAX)
        {
            TraceEvents(TRACE_LEVEL_ERROR, DBG_GENERAL, "invalid center_offset %d", *in_buf);
            status = STATUS_INVALID_PARAMETER;
            break;
        }

        TraceEvents(TRACE_LEVEL_INFORMATION, DBG_GENERAL, "setting center_center to %d", *in_buf);
        dev_ctx->attrs.center_offset = *in_buf;
        cx_set_center_offset(dev_ctx);
        break;

    default:
        status = STATUS_INVALID_DEVICE_REQUEST;
        break;
    }

    WdfRequestComplete(req, status);
}

VOID cx_evt_io_read(
    _In_ WDFQUEUE queue,
    _In_ WDFREQUEST req,
    _In_ size_t req_len
)
{
    NTSTATUS status = STATUS_SUCCESS;
    PDEVICE_CONTEXT dev_ctx = cx_device_get_ctx(WdfIoQueueGetDevice(queue));

    if (!dev_ctx->state.is_capturing)
    {
        KeClearEvent(&dev_ctx->isr_event);

        cx_start_capture(dev_ctx);

        status = KeWaitForSingleObject(&dev_ctx->isr_event, Executive, KernelMode, FALSE, NULL);

        if (!NT_SUCCESS(status))
        {
            TraceEvents(TRACE_LEVEL_ERROR, DBG_GENERAL, "KeWaitForSingleObject failed with status %!STATUS!", status);
        }

        InterlockedExchange(&dev_ctx->state.initial_page, dev_ctx->state.last_gp_cnt);
        InterlockedExchange64(&dev_ctx->state.read_offset, 0);
    }

    WDFMEMORY mem;
    status = WdfRequestRetrieveOutputMemory(req, &mem);

    if (!NT_SUCCESS(status))
    {
        TraceEvents(TRACE_LEVEL_ERROR, DBG_GENERAL, "WdfRequestRetrieveOutputMemory failed with status %!STATUS!", status);
        WdfRequestComplete(req, STATUS_UNSUCCESSFUL);
        return;
    }

    LONG64 count = req_len;
    LONG64 offset = dev_ctx->state.read_offset;
    LONG64 tgt_off = 0;
    LONG page_no = cx_get_page_no(dev_ctx->state.initial_page, offset);

    while (count && dev_ctx->state.is_capturing)
    {
        while (count > 0 && page_no != dev_ctx->state.last_gp_cnt)
        {
            LONG64 page_off = offset % PAGE_SIZE;
            LONG64 len = page_off ? (PAGE_SIZE - page_off) : PAGE_SIZE;

            if (len > count) {
                len = count;
            }

            WdfMemoryCopyFromBuffer(mem, tgt_off, &dev_ctx->dma_risc_page[page_no].va[page_off], len);

            if (!NT_SUCCESS(status))
            {
                TraceEvents(TRACE_LEVEL_ERROR, DBG_GENERAL, "WdfMemoryCopyFromBuffer failed with status %!STATUS!", status);
                WdfRequestComplete(req, STATUS_UNSUCCESSFUL);
                return;
            }

            RtlZeroMemory(&dev_ctx->dma_risc_page[page_no].va[page_off], len);

            count -= len;
            tgt_off += len;
            offset += len;

            page_no = cx_get_page_no(dev_ctx->state.initial_page, offset);
        }

        // check over/underflow, increment count if set
        if (cx_get_ouflow_state(dev_ctx))
        {
            dev_ctx->state.ouflow_count += 1;
            cx_reset_ouflow_state(dev_ctx);
        }

        if (count)
        {
            KeClearEvent(&dev_ctx->isr_event);

            // gp_cnt == page_no but read buffer is not filled
            // wait for interrupt to trigger and continue
            status = KeWaitForSingleObject(&dev_ctx->isr_event, Executive, KernelMode, FALSE, NULL);

            if (!NT_SUCCESS(status))
            {
                TraceEvents(TRACE_LEVEL_ERROR, DBG_GENERAL, "KeWaitForSingleObject failed with status %!STATUS!", status);
                WdfRequestComplete(req, STATUS_UNSUCCESSFUL);
                return;
            }
        }
    }

    // our read request does not contain an offset,
    // so we keep track of it for the duration of the capture
    InterlockedExchange64(&dev_ctx->state.read_offset, offset);

    WdfRequestCompleteWithInformation(req, status, req_len - count);
}

__inline
ULONG cx_get_page_no(
    _In_ ULONG initial_page,
    _In_ size_t off
)
{
    return (((off % CX_VBI_BUF_SIZE) / PAGE_SIZE) + initial_page) % CX_VBI_BUF_COUNT;
}
